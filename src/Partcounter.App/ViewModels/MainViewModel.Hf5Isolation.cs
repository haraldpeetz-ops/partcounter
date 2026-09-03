using System.Windows;
using System.Windows.Input;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

/// <summary>
/// R001.25 HF5 – harte Trennung von Simulation und Echtbetrieb.
///
/// Grundsatz:
/// - Simulation und Echtbetrieb besitzen getrennte MachineState-Instanzen.
/// - Nur der jeweils aktive Satz wird über Machines/VisibleMachines/CompactMachines angezeigt.
/// - Recovery, JobId, HoldAfterVE und Modbus-Sessionzustände werden ausschließlich im
///   Echtbetrieb aktiv gehalten.
/// - Simulations-VE werden niemals über den produktiven MachineOnVeCompleted-Pfad
///   persistiert oder gedruckt.
/// - Neue PackagingUnits werden zusätzlich per SQLite-Trigger nach Betriebsart klassifiziert.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly List<MachineState> _hf5SimulationMachines = new();
    private readonly List<MachineState> _hf5LiveMachines = new();

    private readonly Dictionary<int, ActiveOrderCheckpoint> _hf5ParkedLiveCheckpoints = new();
    private readonly Dictionary<int, uint> _hf5ParkedLiveJobIds = new();
    private readonly Dictionary<int, ushort> _hf5ParkedLiveCompletionHolds = new();
    private readonly HashSet<int> _hf5ParkedLiveManualVePending = new();
    private readonly HashSet<int> _hf5ParkedLiveRecoveryMachines = new();
    private readonly OperatingModeDataIsolationService _hf5DataIsolation = new();

    private ICommand? _hf5ToggleOperatingModeCommand;
    private bool _hf5IsolationEnabled;
    private bool _hf5ModeSwitchInProgress;
    private long _hf5HiddenNonProductionHistoryCount;

    public ICommand Hf5ToggleOperatingModeCommand =>
        _hf5ToggleOperatingModeCommand ??= new AsyncRelayCommand(_ => ToggleOperatingModeHf5Async());

    public bool Hf5IsolationEnabled => _hf5IsolationEnabled;

    public bool Hf5IsUsingSimulationMachines =>
        _hf5IsolationEnabled && UsesMachineSet(_hf5SimulationMachines);

    public bool Hf5IsUsingLiveMachines =>
        _hf5IsolationEnabled && UsesMachineSet(_hf5LiveMachines);

    public int PendingLiveRecoveryCount => IsSimulationMode
        ? _hf5ParkedLiveRecoveryMachines.Count
        : _startupRecoveryMachines.Count;

    public long Hf5HiddenNonProductionHistoryCount => _hf5HiddenNonProductionHistoryCount;

    /// <summary>
    /// Wird nach der normalen MainViewModel-Initialisierung einmalig aufgerufen.
    /// Der bis dahin geladene Maschinenbestand wird als Echtbetriebsbestand übernommen,
    /// weil dort auch eventuell vorhandene Recovery-Aufträge eingelesen wurden.
    /// Anschließend wird ein vollständig unabhängiger Simulationsbestand aufgebaut.
    /// </summary>
    public async Task EnableHf5IsolationAsync()
    {
        if (_hf5IsolationEnabled)
            return;

        if (Machines.Count == 0)
            throw new InvalidOperationException("HF5-Isolation kann erst nach dem Laden der Maschinen aktiviert werden.");

        var selectedMachineNumber = SelectedMachine?.Configuration.MachineNumber;

        _hf5LiveMachines.Clear();
        _hf5LiveMachines.AddRange(Machines);

        _hf5SimulationMachines.Clear();
        foreach (var live in _hf5LiveMachines.OrderBy(m => m.Configuration.MachineNumber))
        {
            var simulation = new MachineState
            {
                Configuration = live.Configuration,
                SimulatedCycleTimeSeconds = 4.0 + live.Configuration.MachineNumber % 11,
                NextSimulatedCycleLocal = DateTime.Now.AddMilliseconds(live.Configuration.MachineNumber * 90),
                ConnectionState = ConnectionState.Simulation
            };

            // Wichtig: NICHT an MachineOnVeCompleted hängen. Dieser Handler ist der produktive
            // Persistenz-/Etikettendruckpfad und darf in der Simulation niemals aufgerufen werden.
            simulation.VeCompleted += Hf5SimulationMachineOnVeCompleted;
            simulation.PropertyChanged += MachineOnPropertyChanged;
            _hf5SimulationMachines.Add(simulation);
        }

        // Der ursprüngliche Fleet-Handler sucht in Machines und könnte dadurch nach einem
        // späten Dispatcher-Callback einen Live-Snapshot auf ein Simulationsobjekt anwenden.
        // HF5 ersetzt ihn durch einen strikt live-spezifischen Handler.
        _fleet.SnapshotReceived -= FleetOnSnapshotReceived;
        _fleet.ConnectionChanged -= FleetOnConnectionChanged;
        _fleet.SnapshotReceived += Hf5FleetOnSnapshotReceived;
        _fleet.ConnectionChanged += Hf5FleetOnConnectionChanged;

        await _hf5DataIsolation.InitializeAsync();
        await FilterProductionHistoryAsync();

        ParkActiveLiveControlState();
        ClearActiveModeControlState();

        SwitchVisibleMachineSet(_hf5SimulationMachines, selectedMachineNumber);
        IsSimulationMode = true;
        foreach (var machine in _hf5SimulationMachines)
            machine.ConnectionState = ConnectionState.Simulation;

        _hf5IsolationEnabled = true;
        OnPropertyChanged(nameof(Hf5IsolationEnabled));
        OnPropertyChanged(nameof(Hf5IsUsingSimulationMachines));
        OnPropertyChanged(nameof(Hf5IsUsingLiveMachines));
        OnPropertyChanged(nameof(PendingLiveRecoveryCount));
        OnPropertyChanged(nameof(Hf5HiddenNonProductionHistoryCount));

        var historyNote = _hf5HiddenNonProductionHistoryCount > 0
            ? $" {_hf5HiddenNonProductionHistoryCount:N0} ältere/unproduktive VE-Datensätze werden aus der HF5-Produktionshistorie ausgeblendet."
            : string.Empty;

        StatusMessage = _hf5ParkedLiveRecoveryMachines.Count > 0
            ? $"HF5-Simulation aktiv und vollständig vom Echtbetrieb getrennt. {_hf5ParkedLiveRecoveryMachines.Count} Echtbetrieb-Recovery-Auftrag/Aufträge sind separat geparkt und beeinflussen die Simulation nicht.{historyNote}"
            : $"HF5-Simulation aktiv und vollständig vom Echtbetrieb getrennt. Keine Modbus-Sessions, keine Produktionshistorie und kein automatischer Produktionsetikettendruck.{historyNote}";
    }

    private async Task FilterProductionHistoryAsync()
    {
        var productionIds = await _hf5DataIsolation.LoadProductionPackagingUnitIdsAsync();
        for (var index = RecentPackagingUnits.Count - 1; index >= 0; index--)
        {
            if (!productionIds.Contains(RecentPackagingUnits[index].Id))
                RecentPackagingUnits.RemoveAt(index);
        }

        var counts = await _hf5DataIsolation.GetHistoryClassificationCountsAsync();
        _hf5HiddenNonProductionHistoryCount = counts.Simulation + counts.LegacyUnknown;
    }

    private async Task ToggleOperatingModeHf5Async()
    {
        if (!_hf5IsolationEnabled)
        {
            StatusMessage = "Betriebsarten-Isolation wird noch initialisiert.";
            return;
        }

        if (_hf5ModeSwitchInProgress)
        {
            StatusMessage = "Betriebsartwechsel läuft bereits.";
            return;
        }

        _hf5ModeSwitchInProgress = true;
        try
        {
            if (IsSimulationMode)
                await ActivateLiveModeHf5Async();
            else
                await ActivateSimulationModeHf5Async();
        }
        finally
        {
            _hf5ModeSwitchInProgress = false;
        }
    }

    private async Task ActivateLiveModeHf5Async()
    {
        var previouslySelectedMachine = SelectedMachine?.Configuration.MachineNumber;

        // Simulationsinterne Hilfswerte werden verworfen. Danach werden ausschließlich die
        // geparkten Echtbetriebs-Recovery-/Job-/Hold-Daten wieder aktiv geschaltet.
        ClearActiveModeControlState();
        RestoreParkedLiveControlState();

        var activationPlan = OperatingModeActivationPolicy.Build(_hf5LiveMachines, _startupRecoveryMachines);
        if (activationPlan.LiveMachines.Count == 0)
        {
            ParkActiveLiveControlState();
            ClearActiveModeControlState();
            StatusMessage = "Echtbetrieb NICHT aktiviert: Keine LOGO!-Station ist administrativ freigegeben. Die Simulation bleibt unverändert aktiv.";
            return;
        }

        foreach (var machine in _hf5LiveMachines)
            machine.ConnectionState = ConnectionState.Offline;

        var fleetStarted = false;
        try
        {
            await _fleet.StartAsync(
                activationPlan.LiveMachines.Select(m => m.Configuration),
                publishSnapshots: false);
            fleetStarted = true;
            await _hf5DataIsolation.SetModeAsync(OperatingModeDataIsolationService.ProductionMode);

            // Erst nachdem der Session-Verbund existiert, wird die aktive Ansicht auf die
            // getrennten Echtbetriebsobjekte umgeschaltet. Die Simulationsobjekte bleiben
            // unverändert im Hintergrund erhalten.
            SwitchVisibleMachineSet(_hf5LiveMachines, previouslySelectedMachine);
            IsSimulationMode = false;
            OnPropertyChanged(nameof(Hf5IsUsingSimulationMachines));
            OnPropertyChanged(nameof(Hf5IsUsingLiveMachines));
            OnPropertyChanged(nameof(PendingLiveRecoveryCount));

            await Task.WhenAll(
                activationPlan.LiveMachines
                    .Where(m => m.IsTemporarilyDisabled)
                    .Select(async machine =>
                    {
                        await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                        await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                    }));

            var recoveryBefore = _startupRecoveryMachines.ToHashSet();
            var recoveryErrors = await ReconcilePendingLiveOrdersAsync();
            var failedRecoveryMachines = LiveModeRecoveryPolicy.ExtractFailedMachineNumbers(recoveryErrors);

            foreach (var machineNumber in recoveryBefore)
            {
                if (!failedRecoveryMachines.Contains(machineNumber))
                    _startupRecoveryMachines.Remove(machineNumber);
            }

            await Task.WhenAll(
                activationPlan.LiveMachines
                    .Where(m => !m.IsTemporarilyDisabled)
                    .Select(machine => _fleet.SetSnapshotPublishingEnabledAsync(
                        machine.Configuration.MachineNumber,
                        enabled: true)));

            var notes = new List<string>();
            if (activationPlan.AdministrativelyDisabledCount > 0)
                notes.Add($"{activationPlan.AdministrativelyDisabledCount} administrativ deaktivierte Station(en)");
            if (activationPlan.DisabledRecoveryMachineNumbers.Count > 0)
                notes.Add($"Recovery administrativ blockiert: {string.Join(", ", activationPlan.DisabledRecoveryMachineNumbers.Select(n => $"M{n:00}"))}");
            if (recoveryErrors.Count > 0)
                notes.Add($"Recovery ungeklärt: {string.Join(" | ", recoveryErrors)}");

            var suffix = notes.Count == 0 ? string.Empty : $" · {string.Join(" · ", notes)}";
            StatusMessage = _startupRecoveryMachines.Count > 0
                ? $"ECHTBETRIEB aktiv und von der Simulation getrennt. {_startupRecoveryMachines.Count} Recovery-Maschine(n) bleiben sicher gesperrt/pausiert; Diagnose und übrige Stationen bleiben verfügbar.{suffix}"
                : $"ECHTBETRIEB aktiv. {activationPlan.LiveMachines.Count} LOGO!-Station(en) initialisiert; Protocol V{ModbusRegisterMap.ProtocolVersion}. Simulationszustände bleiben separat eingefroren.{suffix}";
        }
        catch (Exception ex)
        {
            if (fleetStarted)
            {
                // Ist der Fleet-Verbund bereits aufgebaut, bleibt die Bedienentscheidung
                // ECHTBETRIEB erhalten. Einzelne Kommunikations-/Recoveryfehler werden
                // stationsbezogen diagnostiziert und dürfen die Simulation nicht aktivieren.
                IsSimulationMode = false;
                StatusMessage = $"ECHTBETRIEB bleibt aktiv; Initialisierung/Recovery meldete: {ex.Message} Betroffene Echtaufträge bleiben gesperrt. Die Simulation bleibt getrennt und unverändert.";
                return;
            }

            try { await _fleet.StopAsync(); } catch { }
            try { await _hf5DataIsolation.SetModeAsync(OperatingModeDataIsolationService.SimulationMode); } catch { }
            ParkActiveLiveControlState();
            ClearActiveModeControlState();
            SwitchVisibleMachineSet(_hf5SimulationMachines, previouslySelectedMachine);
            IsSimulationMode = true;
            foreach (var machine in _hf5SimulationMachines)
                machine.ConnectionState = ConnectionState.Simulation;
            OnPropertyChanged(nameof(Hf5IsUsingSimulationMachines));
            OnPropertyChanged(nameof(Hf5IsUsingLiveMachines));
            OnPropertyChanged(nameof(PendingLiveRecoveryCount));
            StatusMessage = $"Echtbetrieb konnte vor Aufbau der Modbus-Sessions nicht aktiviert werden: {ex.Message} Simulation bleibt vollständig getrennt aktiv.";
        }
    }

    private async Task ActivateSimulationModeHf5Async()
    {
        // Verifizierte aktive Echtaufträge dürfen nicht durch einen Betriebsartenwechsel
        // verdeckt werden. Ungeklärte Recovery-Aufträge hingegen dürfen geparkt werden,
        // damit die Simulation unabhängig für Tests verfügbar bleibt.
        var verifiedActiveOrders = _hf5LiveMachines
            .Where(m => m.IsActiveOrder && !_startupRecoveryMachines.Contains(m.Configuration.MachineNumber))
            .OrderBy(m => m.Configuration.MachineNumber)
            .ToList();

        if (verifiedActiveOrders.Count > 0)
        {
            StatusMessage = $"Wechsel zur Simulation gesperrt: {string.Join(", ", verifiedActiveOrders.Select(m => $"M{m.Configuration.MachineNumber:00}"))} besitzt/ besitzen einen verifizierten laufenden oder pausierten Echtauftrag. Diese zuerst kontrolliert beenden.";
            return;
        }

        var selectedMachineNumber = SelectedMachine?.Configuration.MachineNumber;
        string? stopError = null;
        try
        {
            await _fleet.StopAsync();
        }
        catch (Exception ex)
        {
            stopError = ex.Message;
        }

        try
        {
            await _hf5DataIsolation.SetModeAsync(OperatingModeDataIsolationService.SimulationMode);
        }
        catch (Exception ex)
        {
            stopError = string.IsNullOrWhiteSpace(stopError)
                ? $"Datenmodus konnte nicht auf Simulation gesetzt werden: {ex.Message}"
                : $"{stopError} | Datenmodus: {ex.Message}";
        }

        ParkActiveLiveControlState();
        ClearActiveModeControlState();

        // Solange IsSimulationMode noch false ist, kann der 200-ms-Simulationstimer beim
        // Collection-Swap keine Zyklen erzeugen. Erst nach vollständigem Swap wird Simulation freigegeben.
        SwitchVisibleMachineSet(_hf5SimulationMachines, selectedMachineNumber);
        foreach (var machine in _hf5SimulationMachines)
            machine.ConnectionState = ConnectionState.Simulation;
        IsSimulationMode = true;

        OnPropertyChanged(nameof(Hf5IsUsingSimulationMachines));
        OnPropertyChanged(nameof(Hf5IsUsingLiveMachines));
        OnPropertyChanged(nameof(PendingLiveRecoveryCount));

        var recoveryNote = _hf5ParkedLiveRecoveryMachines.Count > 0
            ? $" {_hf5ParkedLiveRecoveryMachines.Count} ungeklärte Echtbetrieb-Recovery-Auftrag/Aufträge bleiben separat geparkt."
            : string.Empty;
        var stopNote = string.IsNullOrWhiteSpace(stopError)
            ? string.Empty
            : $" Diagnosehinweis beim Beenden der Live-Domäne: {stopError}";

        StatusMessage = $"SIMULATION aktiv und vollständig vom Echtbetrieb getrennt. Keine Modbus-Schreibbefehle, keine Produktionshistorie, kein automatischer Produktionsetikettendruck.{recoveryNote}{stopNote}";
    }

    private void Hf5SimulationMachineOnVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine || !_hf5SimulationMachines.Contains(machine))
            return;

        OnUiThread(() =>
        {
            SelectedMachine = machine;
            StatusMessage = $"SIMULATION {machine.DisplayName}: VE {e.VeNumber} abgeschlossen mit {e.Quantity:N0} Teilen. Nur In-Memory-Simulation – kein Produktionsdatensatz und kein Auto-Etikett.";
        });
    }

    private void Hf5FleetOnSnapshotReceived(object? sender, MachineSnapshotEventArgs e)
    {
        if (!_hf5IsolationEnabled || IsSimulationMode)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _ = dispatcher.BeginInvoke(() =>
        {
            if (IsSimulationMode || !Hf5IsUsingLiveMachines)
                return;

            var machine = _hf5LiveMachines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            machine?.ApplyLogoSnapshot(e.Snapshot);
        });
    }

    private void Hf5FleetOnConnectionChanged(object? sender, MachineConnectionEventArgs e)
    {
        if (!_hf5IsolationEnabled || IsSimulationMode)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _ = dispatcher.BeginInvoke(() =>
        {
            if (IsSimulationMode || !Hf5IsUsingLiveMachines)
                return;

            var machine = _hf5LiveMachines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            if (machine is null)
                return;

            machine.ConnectionState = e.State;
            if (e.State == ConnectionState.Offline && !string.IsNullOrWhiteSpace(e.Message))
                StatusMessage = $"{machine.DisplayName} offline: {e.Message}";
        });
    }

    private void ParkActiveLiveControlState()
    {
        CopyDictionary(_liveOrderCheckpoints, _hf5ParkedLiveCheckpoints);
        CopyDictionary(_activeJobIds, _hf5ParkedLiveJobIds);
        CopyDictionary(_scheduledCompletionHolds, _hf5ParkedLiveCompletionHolds);

        _hf5ParkedLiveManualVePending.Clear();
        _hf5ParkedLiveManualVePending.UnionWith(_manualVeReconfigurationPending);

        _hf5ParkedLiveRecoveryMachines.Clear();
        _hf5ParkedLiveRecoveryMachines.UnionWith(_startupRecoveryMachines);
    }

    private void RestoreParkedLiveControlState()
    {
        CopyDictionary(_hf5ParkedLiveCheckpoints, _liveOrderCheckpoints);
        CopyDictionary(_hf5ParkedLiveJobIds, _activeJobIds);
        CopyDictionary(_hf5ParkedLiveCompletionHolds, _scheduledCompletionHolds);

        _manualVeReconfigurationPending.Clear();
        _manualVeReconfigurationPending.UnionWith(_hf5ParkedLiveManualVePending);

        _startupRecoveryMachines.Clear();
        _startupRecoveryMachines.UnionWith(_hf5ParkedLiveRecoveryMachines);
    }

    private void ClearActiveModeControlState()
    {
        _liveOrderCheckpoints.Clear();
        _activeJobIds.Clear();
        _scheduledCompletionHolds.Clear();
        _manualVeReconfigurationPending.Clear();
        _startupRecoveryMachines.Clear();
    }

    private void SwitchVisibleMachineSet(IReadOnlyList<MachineState> source, int? preferredMachineNumber)
    {
        Machines.Clear();
        foreach (var machine in source.OrderBy(m => m.Configuration.MachineNumber))
            Machines.Add(machine);

        SelectedMachine = preferredMachineNumber.HasValue
            ? source.FirstOrDefault(m => m.Configuration.MachineNumber == preferredMachineNumber.Value) ?? source.FirstOrDefault()
            : source.FirstOrDefault();

        RefreshMachineCollections();
    }

    private bool UsesMachineSet(IReadOnlyList<MachineState> expected)
    {
        if (Machines.Count != expected.Count)
            return false;

        for (var i = 0; i < Machines.Count; i++)
        {
            if (!ReferenceEquals(Machines[i], expected[i]))
                return false;
        }
        return true;
    }

    private static void CopyDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        IDictionary<TKey, TValue> destination)
        where TKey : notnull
    {
        destination.Clear();
        foreach (var pair in source)
            destination[pair.Key] = pair.Value;
    }
}
