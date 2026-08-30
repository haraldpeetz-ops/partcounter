using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly DatabaseService _database = new();
    private readonly LabelPrintService _labelPrinter = new();
    private readonly MachineFleetService _fleet = new();
    private readonly DispatcherTimer _simulationTimer;
    private readonly Dictionary<int, ushort> _scheduledCompletionHolds = new();

    private bool _isSimulationMode = true;
    private bool _showIdleMachines;
    private MachineState? _selectedMachine;
    private ArticleDefinition? _selectedArticle;
    private string _orderNumber = $"AUF-{DateTime.Now:yyyyMMdd}-001";
    private uint _orderTargetQuantity = 10000;
    private string _statusMessage = "Initialisierung …";
    private string _labelPrinterName = string.Empty;
    private bool _autoPrintLabels = true;
    private ushort _valvePulseMs = 750;

    private string _editArticleNumber = string.Empty;
    private string _editArticleDescription = string.Empty;
    private string _editToolNumber = string.Empty;
    private ushort _editCavities = 1;
    private uint _editPackagingQuantity = 1000;

    public MainViewModel()
    {
        ApplyArticleCommand = new AsyncRelayCommand(_ => ApplySelectedArticleAsync());
        SaveArticleCommand = new AsyncRelayCommand(_ => SaveArticleAsync());
        ToggleOperatingModeCommand = new AsyncRelayCommand(_ => ToggleOperatingModeAsync());
        PauseOrderCommand = new AsyncRelayCommand(_ => PauseSelectedOrderAsync());
        ResumeOrderCommand = new AsyncRelayCommand(_ => ResumeSelectedOrderAsync());
        EndOrderCommand = new AsyncRelayCommand(_ => EndSelectedOrderAsync());
        ToggleMachineDisabledCommand = new AsyncRelayCommand(_ => ToggleSelectedMachineDisabledAsync());

        AddCycleCommand = new RelayCommand(machine =>
        {
            if (IsSimulationMode && machine is MachineState state)
                state.ApplySimulationCycle();
        });

        ManualVeChangeCommand = new AsyncRelayCommand(ManualVeChangeAsync);
        ResetMachineCommand = new AsyncRelayCommand(ResetMachineAsync);
        SavePrintSettingsCommand = new AsyncRelayCommand(_ => SavePrintSettingsAsync());
        TestLabelCommand = new AsyncRelayCommand(_ => TestLabelAsync());
        CopySelectedArticleToEditorCommand = new RelayCommand(_ => CopySelectedArticleToEditor());

        _fleet.SnapshotReceived += FleetOnSnapshotReceived;
        _fleet.ConnectionChanged += FleetOnConnectionChanged;

        _simulationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _simulationTimer.Tick += (_, _) => SimulationTick();
        _simulationTimer.Start();
    }

    public ObservableCollection<MachineState> Machines { get; } = new();
    public ObservableCollection<MachineState> VisibleMachines { get; } = new();
    public ObservableCollection<MachineState> CompactMachines { get; } = new();
    public ObservableCollection<ArticleDefinition> Articles { get; } = new();
    public ObservableCollection<PackagingUnitRecord> RecentPackagingUnits { get; } = new();

    public ICommand ApplyArticleCommand { get; }
    public ICommand SaveArticleCommand { get; }
    public ICommand ToggleOperatingModeCommand { get; }
    public ICommand PauseOrderCommand { get; }
    public ICommand ResumeOrderCommand { get; }
    public ICommand EndOrderCommand { get; }
    public ICommand ToggleMachineDisabledCommand { get; }
    public ICommand AddCycleCommand { get; }
    public ICommand ManualVeChangeCommand { get; }
    public ICommand ResetMachineCommand { get; }
    public ICommand SavePrintSettingsCommand { get; }
    public ICommand TestLabelCommand { get; }
    public ICommand CopySelectedArticleToEditorCommand { get; }

    public MachineState? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (!SetField(ref _selectedMachine, value)) return;
            OnPropertyChanged(nameof(SelectedMachineDisableButtonText));
            OnPropertyChanged(nameof(SelectedMachineStateText));
        }
    }

    public ArticleDefinition? SelectedArticle
    {
        get => _selectedArticle;
        set => SetField(ref _selectedArticle, value);
    }

    public string OrderNumber
    {
        get => _orderNumber;
        set => SetField(ref _orderNumber, value);
    }

    public uint OrderTargetQuantity
    {
        get => _orderTargetQuantity;
        set => SetField(ref _orderTargetQuantity, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        private set
        {
            if (!SetField(ref _isSimulationMode, value)) return;
            OnPropertyChanged(nameof(OperatingModeButtonText));
            OnPropertyChanged(nameof(SystemStatusText));
        }
    }

    public bool ShowIdleMachines
    {
        get => _showIdleMachines;
        set
        {
            if (!SetField(ref _showIdleMachines, value)) return;
            RefreshMachineCollections();
        }
    }

    public string OperatingModeButtonText => IsSimulationMode ? "Echtbetrieb aktivieren" : "Simulation aktivieren";
    public string SystemStatusText => IsSimulationMode ? AppVersionInfo.SimulationStatus : AppVersionInfo.ProductionStatus;
    public string DatabasePath => _database.DatabasePath;
    public string ActiveMachineSummary => $"{CompactMachines.Count} aktive Maschinen";
    public string SelectedMachineDisableButtonText => SelectedMachine?.IsTemporarilyDisabled == true
        ? "Maschine aktivieren"
        : "Temporär deaktivieren";
    public string SelectedMachineStateText => SelectedMachine is null
        ? "Keine Maschine ausgewählt"
        : $"{SelectedMachine.OrderStatusText} · {SelectedMachine.TemporaryStateText}".TrimEnd(' ', '·');

    public string LabelPrinterName
    {
        get => _labelPrinterName;
        set => SetField(ref _labelPrinterName, value);
    }

    public bool AutoPrintLabels
    {
        get => _autoPrintLabels;
        set => SetField(ref _autoPrintLabels, value);
    }

    public ushort ValvePulseMs
    {
        get => _valvePulseMs;
        set => SetField(ref _valvePulseMs, value);
    }

    public string EditArticleNumber
    {
        get => _editArticleNumber;
        set => SetField(ref _editArticleNumber, value);
    }

    public string EditArticleDescription
    {
        get => _editArticleDescription;
        set => SetField(ref _editArticleDescription, value);
    }

    public string EditToolNumber
    {
        get => _editToolNumber;
        set => SetField(ref _editToolNumber, value);
    }

    public ushort EditCavities
    {
        get => _editCavities;
        set => SetField(ref _editCavities, value);
    }

    public uint EditPackagingQuantity
    {
        get => _editPackagingQuantity;
        set => SetField(ref _editPackagingQuantity, value);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        Machines.Clear();
        foreach (var configuration in await _database.LoadMachinesAsync())
        {
            var machine = new MachineState
            {
                Configuration = configuration,
                SimulatedCycleTimeSeconds = 4.0 + configuration.MachineNumber % 11,
                NextSimulatedCycleLocal = DateTime.Now.AddMilliseconds(configuration.MachineNumber * 90),
                ConnectionState = ConnectionState.Simulation
            };

            machine.VeCompleted += MachineOnVeCompleted;
            machine.PropertyChanged += MachineOnPropertyChanged;
            Machines.Add(machine);
        }

        await ReloadArticlesAsync();

        RecentPackagingUnits.Clear();
        foreach (var record in await _database.LoadRecentPackagingUnitsAsync())
            RecentPackagingUnits.Add(record);

        LabelPrinterName = await _database.GetSettingAsync("LabelPrinterName") ?? string.Empty;
        AutoPrintLabels = bool.TryParse(await _database.GetSettingAsync("AutoPrintLabels"), out var autoPrint) && autoPrint;

        SelectedMachine = Machines.FirstOrDefault();
        SelectedArticle = Articles.FirstOrDefault();

        RefreshMachineCollections();
        StatusMessage = $"Bereit · {Machines.Count} Maschinen · {Articles.Count} Artikel · aktive Maschinen ohne Auftrag werden ausgeblendet.";
    }

    private async Task ToggleOperatingModeAsync()
    {
        if (Machines.Any(m => m.IsActiveOrder))
        {
            StatusMessage = "Betriebsartwechsel gesperrt: Laufende oder pausierte Aufträge zuerst kontrolliert beenden. Simulation und Echtbetrieb dürfen keinen unterschiedlichen Auftragszustand übernehmen.";
            return;
        }

        if (IsSimulationMode)
        {
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Offline;

            await _fleet.StartAsync(Machines.Select(m => m.Configuration));
            foreach (var machine in Machines.Where(m => m.IsTemporarilyDisabled))
                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);

            IsSimulationMode = false;
            StatusMessage = "Echtbetrieb aktiv: Partcounter verbindet parallel mit allen freigegebenen LOGO!-Stationen. Protocol V3 wird zwingend geprüft.";
        }
        else
        {
            await _fleet.StopAsync();
            IsSimulationMode = true;
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Simulation;
            StatusMessage = "Simulation aktiv. Es werden keine Modbus-Schreibbefehle an LOGO! gesendet.";
        }
    }

    private async Task ApplySelectedArticleAsync()
    {
        if (SelectedMachine is null || SelectedArticle is null)
        {
            StatusMessage = "Bitte Maschine und Artikel auswählen.";
            return;
        }

        if (SelectedMachine.IsActiveOrder)
        {
            StatusMessage = $"{SelectedMachine.DisplayName}: Es läuft bereits ein Auftrag. Bitte zuerst pausieren/beenden.";
            return;
        }

        if (SelectedArticle.ActiveCavities is < 1 or > 64 || SelectedArticle.PackagingQuantity == 0)
        {
            StatusMessage = "Artikelparameter sind ungültig.";
            return;
        }

        if (OrderTargetQuantity == 0)
        {
            StatusMessage = "Die Auftragsmenge muss größer als 0 sein.";
            return;
        }

        var machine = SelectedMachine;
        var article = SelectedArticle;
        var order = string.IsNullOrWhiteSpace(OrderNumber)
            ? $"AUF-{DateTime.Now:yyyyMMdd-HHmmss}"
            : OrderNumber.Trim();

        VeBoundaryPlan firstPlan;
        try
        {
            firstPlan = VeBoundaryPolicy.Plan(1, 0, OrderTargetQuantity, article.PackagingQuantity, article.ActiveCavities);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag kann nicht sicher geplant werden: {ex.Message}";
            return;
        }

        var wasTemporarilyDisabled = machine.IsTemporarilyDisabled;

        if (!IsSimulationMode)
        {
            if (wasTemporarilyDisabled)
                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);

            var job = new JobParameters(
                StableUInt32(order),
                article.ArticleNumber,
                article.ToolNumber,
                article.ActiveCavities,
                firstPlan.TargetParts,
                firstPlan.TargetCycles,
                ValvePulseMs,
                firstPlan.HoldAfterVeNumber);

            try
            {
                await _fleet.SendJobAsync(machine.Configuration.MachineNumber, job);
            }
            catch (Exception ex)
            {
                if (wasTemporarilyDisabled)
                {
                    try
                    {
                        await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                    }
                    catch
                    {
                    }
                }

                StatusMessage = $"Auftrag nicht übernommen – LOGO!-Protocol-V3-Übertragung fehlgeschlagen: {ex.Message}";
                await _database.AddEventAsync(machine.Configuration.MachineNumber, "MODBUS_WRITE_ERROR", ex.Message);
                return;
            }
        }

        machine.StartOrder(article, order, OrderTargetQuantity);
        _scheduledCompletionHolds[machine.Configuration.MachineNumber] = firstPlan.HoldAfterVeNumber;
        SelectedMachine = machine;

        StatusMessage =
            $"{machine.DisplayName}: Auftrag {order} gestartet · Soll {OrderTargetQuantity:N0} Teile · " +
            $"{machine.RequiredOrderVes:N0} VE geplant · erste VE {machine.CurrentVeTargetParts:N0} Teile · " +
            $"sicherer LOGO!-Grenzhalt nach VE {firstPlan.HoldAfterVeNumber:N0}.";
        await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_STARTED", StatusMessage);
    }

    private async Task PauseSelectedOrderAsync()
    {
        var machine = SelectedMachine;
        if (machine is null || machine.OrderState != ProductionOrderState.Running)
        {
            StatusMessage = "Die ausgewählte Maschine hat keinen laufenden Auftrag.";
            return;
        }

        try
        {
            if (!IsSimulationMode)
                await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);

            machine.PauseOrder();
            StatusMessage = $"{machine.DisplayName}: Auftrag {machine.OrderNumber} pausiert.";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_PAUSED", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag konnte nicht pausiert werden: {ex.Message}";
        }
    }

    private async Task ResumeSelectedOrderAsync()
    {
        var machine = SelectedMachine;
        if (machine is null || machine.OrderState != ProductionOrderState.Paused)
        {
            StatusMessage = "Die ausgewählte Maschine hat keinen pausierten Auftrag.";
            return;
        }

        if (machine.IsTemporarilyDisabled)
        {
            StatusMessage = "Maschine zuerst wieder aktivieren.";
            return;
        }

        try
        {
            if (!IsSimulationMode)
            {
                if (_scheduledCompletionHolds.TryGetValue(machine.Configuration.MachineNumber, out var priorHold) &&
                    priorHold > 0 && machine.CurrentVeNumber > priorHold)
                {
                    var diagnostics = _fleet.GetCommunicationDiagnostics(machine.Configuration.MachineNumber);
                    if (diagnostics is null ||
                        (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)
                        throw new InvalidOperationException("Grenzwiederanlauf gesperrt: LOGO! meldet keinen aktiven Completion-Hold.");

                    var recoveryPlan = VeBoundaryPolicy.Plan(
                        machine.CurrentVeNumber,
                        machine.OrderProducedQuantity,
                        machine.OrderTargetQuantity,
                        machine.TargetPartsPerVe,
                        machine.ActiveCavities);

                    var recoveryJob = new JobParameters(
                        StableUInt32(machine.OrderNumber),
                        machine.ArticleNumber,
                        machine.ToolNumber,
                        machine.ActiveCavities,
                        recoveryPlan.TargetParts,
                        recoveryPlan.TargetCycles,
                        ValvePulseMs,
                        recoveryPlan.HoldAfterVeNumber);

                    await _fleet.UpdateVeTargetAsync(machine.Configuration.MachineNumber, recoveryJob, pauseCounting: true);
                    _scheduledCompletionHolds[machine.Configuration.MachineNumber] = recoveryPlan.HoldAfterVeNumber;
                    await _database.AddEventAsync(machine.Configuration.MachineNumber, "VE_BOUNDARY_RECOVERY_CONFIGURED",
                        $"Grenzwiederanlauf vorbereitet: VE {machine.CurrentVeNumber}, Ziel {recoveryPlan.TargetParts}, nächster Hold {recoveryPlan.HoldAfterVeNumber}.");
                }

                await _fleet.ResumeCountingAsync(machine.Configuration.MachineNumber);
            }

            machine.ResumeOrder();
            StatusMessage = $"{machine.DisplayName}: Auftrag {machine.OrderNumber} fortgesetzt.";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_RESUMED", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag konnte nicht sicher fortgesetzt werden: {ex.Message}";
        }
    }

    private async Task EndSelectedOrderAsync()
    {
        var machine = SelectedMachine;
        if (machine is null || !machine.IsActiveOrder)
        {
            StatusMessage = "Die ausgewählte Maschine hat keinen aktiven Auftrag.";
            return;
        }

        try
        {
            if (!IsSimulationMode)
                await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);

            machine.EndOrder();
            _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);
            StatusMessage =
                $"{machine.DisplayName}: Auftrag {machine.OrderNumber} beendet bei {machine.OrderProducedQuantity:N0} / {machine.OrderTargetQuantity:N0} Teilen.";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_ENDED", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag konnte nicht beendet werden: {ex.Message}";
        }
    }

    private async Task ToggleSelectedMachineDisabledAsync()
    {
        if (SelectedMachine is null)
        {
            StatusMessage = "Bitte Maschine auswählen.";
            return;
        }

        var machine = SelectedMachine;
        var disable = !machine.IsTemporarilyDisabled;

        try
        {
            if (!IsSimulationMode)
            {
                if (disable && machine.OrderState == ProductionOrderState.Running)
                    await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);

                await _fleet.SetMachinePollingEnabledAsync(
                    machine.Configuration.MachineNumber,
                    enabled: !disable);
            }

            machine.SetTemporarilyDisabled(disable);

            StatusMessage = disable
                ? $"{machine.DisplayName} temporär deaktiviert und aus den aktiven Ansichten entfernt."
                : $"{machine.DisplayName} wieder aktiviert. Ein pausierter Auftrag muss bewusst fortgesetzt werden.";

            OnPropertyChanged(nameof(SelectedMachineDisableButtonText));
            OnPropertyChanged(nameof(SelectedMachineStateText));
            RefreshMachineCollections();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Maschinenstatus konnte nicht geändert werden: {ex.Message}";
        }
    }

    private async Task ManualVeChangeAsync(object? parameter)
    {
        if (parameter is not MachineState machine) return;

        if (!machine.IsActiveOrder || machine.IsTemporarilyDisabled)
        {
            StatusMessage = $"{machine.DisplayName}: Kein aktiver Auftrag.";
            return;
        }

        if (IsSimulationMode)
        {
            machine.CompleteCurrentVe(VeCompletionReason.Manual);
            return;
        }

        try
        {
            await _fleet.SendManualVeChangeAsync(machine.Configuration.MachineNumber);
            StatusMessage = $"Manueller VE-Wechsel an {machine.DisplayName} angefordert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"VE-Wechsel fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task ResetMachineAsync(object? parameter)
    {
        if (parameter is not MachineState machine) return;

        if (!machine.IsActiveOrder)
        {
            StatusMessage = $"{machine.DisplayName}: Kein aktiver Auftrag zum Zurücksetzen.";
            return;
        }

        VeBoundaryPlan resetPlan;
        try
        {
            resetPlan = VeBoundaryPolicy.Plan(1, 0, machine.OrderTargetQuantity, machine.TargetPartsPerVe, machine.ActiveCavities);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset kann nicht sicher geplant werden: {ex.Message}";
            return;
        }

        if (IsSimulationMode)
        {
            machine.ResetCounters();
            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;
            StatusMessage = $"{machine.DisplayName}: Auftragszähler zurückgesetzt.";
            return;
        }

        try
        {
            var resetJob = new JobParameters(
                StableUInt32(machine.OrderNumber),
                machine.ArticleNumber,
                machine.ToolNumber,
                machine.ActiveCavities,
                resetPlan.TargetParts,
                resetPlan.TargetCycles,
                ValvePulseMs,
                resetPlan.HoldAfterVeNumber);

            await _fleet.SendJobAsync(machine.Configuration.MachineNumber, resetJob);
            machine.ResetCounters();
            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;
            StatusMessage = $"Reset an {machine.DisplayName} gesendet; Grenzhalt nach VE {resetPlan.HoldAfterVeNumber:N0} bestätigt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task SaveArticleAsync()
    {
        try
        {
            var article = new ArticleDefinition(
                0,
                EditArticleNumber.Trim(),
                EditArticleDescription.Trim(),
                EditToolNumber.Trim(),
                EditCavities,
                EditPackagingQuantity,
                true);

            await _database.UpsertArticleAsync(article);
            await ReloadArticlesAsync(article.ArticleNumber);
            StatusMessage =
                $"Artikel {article.ArticleNumber} gespeichert. Standard-VE {article.PackagingQuantity:N0}, effektiv {article.EffectivePackagingQuantity:N0} Stück.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Artikel konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task ReloadArticlesAsync(string? selectArticleNumber = null)
    {
        Articles.Clear();
        foreach (var article in await _database.LoadArticlesAsync())
            Articles.Add(article);

        if (!string.IsNullOrWhiteSpace(selectArticleNumber))
            SelectedArticle = Articles.FirstOrDefault(a =>
                a.ArticleNumber.Equals(selectArticleNumber, StringComparison.OrdinalIgnoreCase));
        else
            SelectedArticle ??= Articles.FirstOrDefault();
    }

    private void CopySelectedArticleToEditor()
    {
        if (SelectedArticle is null) return;
        EditArticleNumber = SelectedArticle.ArticleNumber;
        EditArticleDescription = SelectedArticle.Description;
        EditToolNumber = SelectedArticle.ToolNumber;
        EditCavities = SelectedArticle.ActiveCavities;
        EditPackagingQuantity = SelectedArticle.PackagingQuantity;
    }

    private async Task SavePrintSettingsAsync()
    {
        await _database.SetSettingAsync("LabelPrinterName", LabelPrinterName.Trim());
        await _database.SetSettingAsync("AutoPrintLabels", AutoPrintLabels.ToString().ToLowerInvariant());
        StatusMessage = "Etikettendruck-Einstellungen gespeichert.";
    }

    private async Task TestLabelAsync()
    {
        if (SelectedMachine is null || SelectedArticle is null)
        {
            StatusMessage = "Für Testetikett bitte Maschine und Artikel auswählen.";
            return;
        }

        var now = DateTime.UtcNow;
        var record = new PackagingUnitRecord(
            $"TEST-{now:yyyyMMddHHmmssfff}",
            SelectedMachine.Configuration.MachineNumber,
            SelectedMachine.Configuration.Name,
            1,
            OrderNumber,
            SelectedArticle.ArticleNumber,
            SelectedArticle.Description,
            SelectedArticle.ToolNumber,
            SelectedArticle.ActiveCavities,
            SelectedArticle.PackagingQuantity,
            SelectedArticle.EffectivePackagingQuantity,
            SelectedArticle.ExpectedOverfill,
            VeCompletionReason.AutomaticFull,
            now,
            "Test",
            null);

        var printed = await _labelPrinter.PrintAsync(record, LabelPrinterName);
        StatusMessage = printed
            ? "Testetikett an Drucker übergeben."
            : "Testetikett nicht gedruckt. Druckername prüfen.";
    }

    private void SimulationTick()
    {
        if (!IsSimulationMode) return;

        var now = DateTime.Now;
        foreach (var machine in Machines)
        {
            if (machine.OrderState != ProductionOrderState.Running || machine.IsTemporarilyDisabled)
                continue;
            if (now < machine.NextSimulatedCycleLocal)
                continue;

            machine.ApplySimulationCycle();
            machine.NextSimulatedCycleLocal = now.AddSeconds(machine.SimulatedCycleTimeSeconds);
        }
    }

    private void FleetOnSnapshotReceived(object? sender, MachineSnapshotEventArgs e)
    {
        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            if (machine is null) return;
            machine.ApplyLogoSnapshot(e.Snapshot);
        });
    }

    private void FleetOnConnectionChanged(object? sender, MachineConnectionEventArgs e)
    {
        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            if (machine is null) return;

            machine.ConnectionState = e.State;
            if (e.State == ConnectionState.Offline && !string.IsNullOrWhiteSpace(e.Message))
                StatusMessage = $"{machine.DisplayName} offline: {e.Message}";
        });
    }

    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        var completedUtc = e.CompletedAtLocal.ToUniversalTime();
        var targetForCompletedVe = Math.Max(1u, e.TargetQuantity);
        var overfill = e.Quantity > targetForCompletedVe ? e.Quantity - targetForCompletedVe : 0;
        var initialStatus = AutoPrintLabels ? "Pending" : "Disabled";
        var atHeldBoundary = false;
        string? boundaryError = null;

        if (!IsSimulationMode)
        {
            (atHeldBoundary, boundaryError) = await PrecheckRealVeBoundaryAsync(machine, e);
        }

        var record = new PackagingUnitRecord(
            $"PC-{completedUtc:yyyyMMddHHmmssfff}-M{machine.Configuration.MachineNumber:00}-VE{e.VeNumber:0000}",
            machine.Configuration.MachineNumber,
            machine.Configuration.Name,
            e.VeNumber,
            machine.OrderNumber,
            machine.ArticleNumber,
            machine.ArticleDescription,
            machine.ToolNumber,
            machine.ActiveCavities,
            targetForCompletedVe,
            e.Quantity,
            overfill,
            e.Reason,
            completedUtc,
            initialStatus,
            null);

        try
        {
            await _database.SavePackagingUnitAsync(record);

            if (AutoPrintLabels)
            {
                var printed = await _labelPrinter.PrintAsync(record, LabelPrinterName);
                var printedAt = printed ? DateTime.UtcNow : (DateTime?)null;
                var labelStatus = printed ? "Printed" : "PendingPrinter";
                await _database.UpdateLabelStatusAsync(record.Id, labelStatus, printedAt);
                record = record with { LabelStatus = labelStatus, PrintedAtUtc = printedAt };
            }

            RecentPackagingUnits.Insert(0, record);
            while (RecentPackagingUnits.Count > 100)
                RecentPackagingUnits.RemoveAt(RecentPackagingUnits.Count - 1);

            if (!string.IsNullOrWhiteSpace(boundaryError))
            {
                StatusMessage = $"SICHERHEITSHALT {machine.DisplayName}: {boundaryError} VE {e.VeNumber} wurde protokolliert; keine Zählfreigabe erteilt.";
                return;
            }

            if (!IsSimulationMode && atHeldBoundary)
                await ContinueAfterHeldBoundaryAsync(machine, e);

            StatusMessage = machine.OrderState == ProductionOrderState.Completed
                ? $"{machine.DisplayName}: VE {e.VeNumber} fertig; Auftrag {machine.OrderNumber} mit {machine.OrderProducedQuantity:N0} Teilen abgeschlossen und LOGO!-Grenzhalt aktiv."
                : $"{machine.DisplayName}: VE {e.VeNumber} fertig mit {e.Quantity:N0} Teilen; nächste VE {machine.CurrentVeTargetParts:N0} Teile; Etikett: {record.LabelStatus}.";
        }
        catch (Exception ex)
        {
            if (!IsSimulationMode && atHeldBoundary && machine.OrderState == ProductionOrderState.Running)
                machine.PauseOrder();
            StatusMessage = $"VE-Abschluss/Sicherheitsgrenze nicht vollständig verarbeitet: {ex.Message}";
        }
    }

    private async Task<(bool AtHeldBoundary, string? Error)> PrecheckRealVeBoundaryAsync(
        MachineState machine,
        VeCompletedEventArgs e)
    {
        var machineNumber = machine.Configuration.MachineNumber;
        if (!_scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduledHold) || scheduledHold == 0)
        {
            const string reason = "Für den aktiven Auftrag fehlt ein geplanter LOGO!-Grenzhalt.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        if (e.VeNumber < scheduledHold)
            return (false, null);

        if (e.VeNumber > scheduledHold)
        {
            var reason = $"VE {e.VeNumber} wurde abgeschlossen, obwohl der Grenzhalt nach VE {scheduledHold} geplant war.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        var diagnostics = _fleet.GetCommunicationDiagnostics(machineNumber);
        if (diagnostics is null ||
            (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)
        {
            var reason = $"LOGO! meldet an der geplanten Grenze VE {scheduledHold} keinen aktiven Completion-Hold.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        return (true, null);
    }

    private async Task ContinueAfterHeldBoundaryAsync(MachineState machine, VeCompletedEventArgs e)
    {
        var machineNumber = machine.Configuration.MachineNumber;

        if (machine.OrderState == ProductionOrderState.Completed)
        {
            await _fleet.PauseCountingAsync(machineNumber);
            _scheduledCompletionHolds.Remove(machineNumber);
            await _database.AddEventAsync(machineNumber, "VE_BOUNDARY_HOLD_FINAL",
                $"Auftrag {machine.OrderNumber}: finaler Grenzhalt nach VE {e.VeNumber} aktiv und Zählpause bestätigt.");
            return;
        }

        if (!machine.IsActiveOrder || machine.CurrentVeTargetParts == 0)
        {
            const string reason = "Nach Grenzhalt ist kein gültiges nächstes VE-Ziel vorhanden.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            throw new InvalidOperationException(reason);
        }

        VeBoundaryPlan nextPlan;
        try
        {
            nextPlan = VeBoundaryPolicy.Plan(
                machine.CurrentVeNumber,
                machine.OrderProducedQuantity,
                machine.OrderTargetQuantity,
                machine.TargetPartsPerVe,
                machine.ActiveCavities);
        }
        catch (Exception ex)
        {
            await EnterBoundaryFailSafeAsync(machine, $"Nächste VE konnte nicht sicher geplant werden: {ex.Message}");
            throw;
        }

        if (nextPlan.TargetParts != machine.CurrentVeTargetParts)
        {
            var reason = $"Planungsabweichung: MachineState erwartet {machine.CurrentVeTargetParts}, Grenzplan {nextPlan.TargetParts} Teile.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            throw new InvalidOperationException(reason);
        }

        var nextJob = new JobParameters(
            StableUInt32(machine.OrderNumber),
            machine.ArticleNumber,
            machine.ToolNumber,
            machine.ActiveCavities,
            nextPlan.TargetParts,
            nextPlan.TargetCycles,
            ValvePulseMs,
            nextPlan.HoldAfterVeNumber);

        try
        {
            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);
            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;
            if (machine.OrderState == ProductionOrderState.Running)
                await _fleet.ResumeCountingAsync(machineNumber);
        }
        catch (Exception ex)
        {
            await EnterBoundaryFailSafeAsync(machine, $"Grenz-Rekonfiguration fehlgeschlagen: {ex.Message}");
            throw;
        }

        await _database.AddEventAsync(machineNumber, "VE_BOUNDARY_RECONFIGURED",
            $"Nach VE {e.VeNumber}: neues Ziel {nextPlan.TargetParts} Teile; nächster sicherer Grenzhalt VE {nextPlan.HoldAfterVeNumber}.");
    }

    private async Task EnterBoundaryFailSafeAsync(MachineState machine, string reason)
    {
        var detail = reason;
        try
        {
            await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);
        }
        catch (Exception pauseError)
        {
            detail += $" Zusätzlicher Pause-Befehl fehlgeschlagen: {pauseError.Message}";
        }

        if (machine.OrderState == ProductionOrderState.Running)
            machine.PauseOrder();

        await _database.AddEventAsync(machine.Configuration.MachineNumber, "SAFETY_VE_BOUNDARY_STOP", detail);
        StatusMessage = $"SICHERHEITSHALT {machine.DisplayName}: {detail}";
    }

    private void MachineOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        if (ReferenceEquals(machine, SelectedMachine) &&
            e.PropertyName is nameof(MachineState.IsTemporarilyDisabled) or nameof(MachineState.OrderState))
        {
            OnUiThread(() =>
            {
                OnPropertyChanged(nameof(SelectedMachineDisableButtonText));
                OnPropertyChanged(nameof(SelectedMachineStateText));
            });
        }

        if (e.PropertyName is nameof(MachineState.OrderState)
            or nameof(MachineState.IsTemporarilyDisabled)
            or nameof(MachineState.HasVeAttention)
            or nameof(MachineState.ShouldShowAsActive))
        {
            OnUiThread(RefreshMachineCollections);
        }
    }

    private void RefreshMachineCollections()
    {
        var visible = Machines
            .Where(m => !m.IsTemporarilyDisabled && (ShowIdleMachines || m.ShouldShowAsActive))
            .OrderBy(m => m.Configuration.MachineNumber)
            .ToList();

        var compact = Machines
            .Where(m => m.ShouldShowAsActive)
            .OrderBy(m => m.Configuration.MachineNumber)
            .ToList();

        ReplaceCollection(VisibleMachines, visible);
        ReplaceCollection(CompactMachines, compact);
        OnPropertyChanged(nameof(ActiveMachineSummary));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        if (collection.SequenceEqual(items))
            return;

        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private static uint StableUInt32(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash == 0 ? 1u : hash;
        }
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    public async ValueTask DisposeAsync()
    {
        _simulationTimer.Stop();

        foreach (var machine in Machines)
        {
            machine.VeCompleted -= MachineOnVeCompleted;
            machine.PropertyChanged -= MachineOnPropertyChanged;
        }

        await _fleet.DisposeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class AsyncRelayCommand(Func<object?, Task> execute) : ICommand
    {
        private bool _running;
        public bool CanExecute(object? parameter) => !_running;

        public async void Execute(object? parameter)
        {
            if (_running) return;

            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await execute(parameter);
            }
            finally
            {
                _running = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanExecuteChanged;
    }
}
