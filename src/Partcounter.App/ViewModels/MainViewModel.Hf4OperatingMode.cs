using System.Windows.Input;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

/// <summary>
/// HF4 runtime correction for the operator-controlled Simulation/Echtbetrieb transition.
/// A communication/recovery failure must block only the affected machine, not tear down
/// every Modbus session and silently return the complete application to simulation.
/// </summary>
public sealed partial class MainViewModel
{
    private ICommand? _hf4ToggleOperatingModeCommand;
    private bool _hf4OperatingModeSwitchInProgress;

    public ICommand Hf4ToggleOperatingModeCommand =>
        _hf4ToggleOperatingModeCommand ??= new AsyncRelayCommand(_ => ToggleOperatingModeHf4Async());

    private async Task ToggleOperatingModeHf4Async()
    {
        if (_hf4OperatingModeSwitchInProgress)
        {
            StatusMessage = "Betriebsartwechsel läuft bereits. Bitte den aktuellen Umschaltvorgang abschließen lassen.";
            return;
        }

        _hf4OperatingModeSwitchInProgress = true;
        try
        {
            if (IsSimulationMode)
                await ActivateLiveModeHf4Async();
            else
                await ActivateSimulationModeHf4Async();
        }
        finally
        {
            _hf4OperatingModeSwitchInProgress = false;
        }
    }

    private async Task ActivateLiveModeHf4Async()
    {
        var activationPlan = OperatingModeActivationPolicy.Build(Machines, _startupRecoveryMachines);

        if (activationPlan.LiveMachines.Count == 0)
        {
            StatusMessage = "Echtbetrieb NICHT aktiviert: In der Maschinen-/Modbus-Konfiguration ist keine LOGO!-Station administrativ aktiviert. Mindestens eine Station aktivieren.";
            return;
        }

        // Pure simulation orders have no authority in a real LOGO session.
        var discardedSimulationOrders = activationPlan.SimulationOrdersToDiscard.Count;
        foreach (var machine in activationPlan.SimulationOrdersToDiscard)
        {
            var discardedOrder = machine.OrderNumber;
            machine.ClearRecoveredOrder();
            _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);
            _manualVeReconfigurationPending.Remove(machine.Configuration.MachineNumber);
            try
            {
                await _database.AddEventAsync(
                    machine.Configuration.MachineNumber,
                    "SIMULATION_ORDER_DISCARDED_FOR_LIVE_MODE",
                    $"Simulationsauftrag {discardedOrder} wurde beim bewussten Wechsel in den Echtbetrieb verworfen.");
            }
            catch
            {
                // Diagnostics must never make the mode transition fail.
            }
        }

        foreach (var machine in Machines)
            machine.ConnectionState = ConnectionState.Offline;

        var fleetStarted = false;
        try
        {
            // StartAsync creates sessions for all administratively enabled stations. It does not
            // require every LOGO to be online. Offline stations remain diagnosable as OFFLINE.
            await _fleet.StartAsync(
                activationPlan.LiveMachines.Select(m => m.Configuration),
                publishSnapshots: false);
            fleetStarted = true;

            // HF4: Commit the explicit operator decision immediately after the session fleet exists.
            // Subsequent station/recovery failures are per-machine faults and MUST NOT flip this flag back.
            IsSimulationMode = false;

            foreach (var machine in activationPlan.LiveMachines.Where(m => m.IsTemporarilyDisabled))
            {
                await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
            }

            var recoveryBefore = _startupRecoveryMachines.ToHashSet();
            var recoveryErrors = await ReconcilePendingLiveOrdersAsync();
            var failedRecoveryMachines = LiveModeRecoveryPolicy.ExtractFailedMachineNumbers(recoveryErrors);

            // ReconcilePendingLiveOrdersAsync removes completed/proven-idle records itself. For the
            // normal verified-paused path we clear the startup lock here. Failed stations stay locked.
            foreach (var machineNumber in recoveryBefore)
            {
                if (!failedRecoveryMachines.Contains(machineNumber))
                    _startupRecoveryMachines.Remove(machineNumber);
            }

            foreach (var machine in activationPlan.LiveMachines.Where(m => !m.IsTemporarilyDisabled))
                await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);

            var notes = new List<string>();
            if (activationPlan.AdministrativelyDisabledCount > 0)
                notes.Add($"{activationPlan.AdministrativelyDisabledCount} administrativ deaktivierte Station(en) ohne Fleet-Session");
            if (discardedSimulationOrders > 0)
                notes.Add($"{discardedSimulationOrders} Simulationsauftrag/Simulationsaufträge kontrolliert verworfen");
            if (activationPlan.DisabledRecoveryMachineNumbers.Count > 0)
                notes.Add($"Recovery auf {string.Join(", ", activationPlan.DisabledRecoveryMachineNumbers.Select(n => $"M{n:00}"))} blockiert, weil Station administrativ deaktiviert ist");
            if (recoveryErrors.Count > 0)
                notes.Add($"Recovery ungeklärt: {string.Join(" | ", recoveryErrors)}");

            var suffix = notes.Count == 0 ? string.Empty : $" · {string.Join(" · ", notes)}";
            var unresolvedCount = _startupRecoveryMachines.Count;

            StatusMessage = unresolvedCount > 0
                ? $"Echtbetrieb BLEIBT aktiv. {activationPlan.LiveMachines.Count} LOGO!-Session(s) initialisiert; {unresolvedCount} Recovery-Maschine(n) bleiben sicher gesperrt/pausiert. Diagnose und Wiederverbindung bleiben verfügbar.{suffix}"
                : $"Echtbetrieb aktiv: {activationPlan.LiveMachines.Count} freigegebene LOGO!-Station(en) initialisiert. Nicht erreichbare Stationen werden einzeln als Offline gemeldet; Protocol V{ModbusRegisterMap.ProtocolVersion} wird bei Kommunikation geprüft.{suffix}";

            try
            {
                await _database.AddEventAsync(
                    null,
                    unresolvedCount > 0 ? "LIVE_MODE_ACTIVE_RECOVERY_BLOCKED" : "LIVE_MODE_ACTIVE",
                    StatusMessage);
            }
            catch
            {
                // Logging must never tear down the live-mode communication fleet.
            }
        }
        catch (Exception ex)
        {
            if (fleetStarted)
            {
                // The fleet already exists. Keep live mode latched so diagnostics can identify and
                // recover the actual station fault. Unresolved recovery machines stay blocked.
                IsSimulationMode = false;
                StatusMessage = $"Echtbetrieb BLEIBT aktiv; Initialisierung/Recovery meldete einen Fehler: {ex.Message} Modbus-Sessions werden nicht verworfen. Betroffene Recovery-Aufträge bleiben gesperrt.";
                try
                {
                    await _database.AddEventAsync(null, "LIVE_MODE_ACTIVE_WITH_ERROR", StatusMessage);
                }
                catch
                {
                }
                return;
            }

            // Only a failure before a communication fleet exists justifies returning to simulation.
            await RollbackLiveModeActivationAsync();
            StatusMessage = $"Echtbetrieb konnte vor Aufbau der Modbus-Sessions nicht aktiviert werden: {ex.Message} Simulation bleibt aktiv.";
            try
            {
                await _database.AddEventAsync(null, "LIVE_MODE_ACTIVATION_FAILED", StatusMessage);
            }
            catch
            {
            }
        }
    }

    private async Task ActivateSimulationModeHf4Async()
    {
        if (Machines.Any(m => m.IsActiveOrder) || _startupRecoveryMachines.Count > 0)
        {
            var recoveryHint = _startupRecoveryMachines.Count > 0
                ? $" {_startupRecoveryMachines.Count} ungeklärte(r) Recovery-Auftrag/Aufträge sind noch gesperrt."
                : string.Empty;
            StatusMessage = $"Betriebsartwechsel gesperrt: Laufende, pausierte oder ungeklärte Echtaufträge zuerst kontrolliert klären/beenden.{recoveryHint}";
            return;
        }

        try
        {
            await _fleet.StopAsync();
            IsSimulationMode = true;
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Simulation;
            StatusMessage = "Simulation aktiv. Es werden keine Modbus-Schreibbefehle an LOGO! gesendet.";
        }
        catch (Exception ex)
        {
            // Teardown errors must never leave a misleading live indicator after the operator
            // explicitly selected simulation. Modbus clients are best-effort disposed by StopAsync.
            IsSimulationMode = true;
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Simulation;
            StatusMessage = $"Simulation aktiviert; beim Beenden der Modbus-Sessions trat ein Diagnosefehler auf: {ex.Message}";
        }
    }
}
