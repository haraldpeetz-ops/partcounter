using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed partial class MainViewModel
{
    private ActiveOrderRecoveryService? _orderRecovery;
    private readonly Dictionary<int, ActiveOrderCheckpoint> _liveOrderCheckpoints = new();
    private readonly Dictionary<int, uint> _activeJobIds = new();
    private readonly HashSet<int> _startupRecoveryMachines = new();

    private ActiveOrderRecoveryService OrderRecovery =>
        _orderRecovery ??= new ActiveOrderRecoveryService(_database.DatabasePath);

    private bool IsPendingStartupRecovery(MachineState machine) =>
        IsSimulationMode && _startupRecoveryMachines.Contains(machine.Configuration.MachineNumber);

    private bool HasUnresolvedPendingActivation(MachineState machine) =>
        _liveOrderCheckpoints.TryGetValue(machine.Configuration.MachineNumber, out var checkpoint) &&
        checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation;

    private async Task LoadPendingLiveOrderRecoveryAsync()
    {
        await OrderRecovery.InitializeAsync();
        var checkpoints = await OrderRecovery.LoadAsync();
        foreach (var checkpoint in checkpoints)
        {
            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == checkpoint.MachineNumber);
            if (machine is null)
            {
                await _database.AddEventAsync(checkpoint.MachineNumber, "RECOVERY_MACHINE_MISSING",
                    $"Recovery-Checkpoint {checkpoint.OrderNumber} konnte keiner konfigurierten Maschine zugeordnet werden.");
                continue;
            }

            machine.RestoreRecoveredOrder(checkpoint);
            _liveOrderCheckpoints[checkpoint.MachineNumber] = checkpoint;
            _activeJobIds[checkpoint.MachineNumber] = checkpoint.JobId;
            _startupRecoveryMachines.Add(checkpoint.MachineNumber);
            if (checkpoint.ScheduledHoldAfterVeNumber > 0)
                _scheduledCompletionHolds[checkpoint.MachineNumber] = checkpoint.ScheduledHoldAfterVeNumber;
            if (checkpoint.ManualVeReconfigurationPending)
                _manualVeReconfigurationPending.Add(checkpoint.MachineNumber);
        }
    }

    private async Task<List<string>> ReconcilePendingLiveOrdersAsync()
    {
        var errors = new List<string>();
        foreach (var machineNumber in _startupRecoveryMachines.OrderBy(x => x).ToList())
        {
            if (!_liveOrderCheckpoints.TryGetValue(machineNumber, out var checkpoint))
                continue;

            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == machineNumber);
            if (machine is null)
            {
                errors.Add($"M{machineNumber:00}: Maschine fehlt.");
                continue;
            }

            try
            {
                await _fleet.SetMachinePollingEnabledAsync(machineNumber, enabled: true);
                var snapshot = await _fleet.ReadSnapshotAsync(machineNumber);

                if (snapshot.JobIdEcho != checkpoint.JobId)
                {
                    if (checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation &&
                        RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(snapshot))
                    {
                        // The pending PC command is definitely not active and the LOGO reports no
                        // autonomous production state. Only in this proven-idle case may we discard it.
                        await _database.AddEventAsync(machineNumber, "RECOVERY_PENDING_START_NOT_ACTIVE",
                            $"Pending-Auftrag {checkpoint.OrderNumber} (JobId {checkpoint.JobId}) wurde nicht aktiv; LOGO! ist nachweislich leer/inaktiv. Checkpoint wird verworfen.");
                        await DeleteLiveOrderCheckpointAsync(machineNumber);
                        _activeJobIds.Remove(machineNumber);
                        _scheduledCompletionHolds.Remove(machineNumber);
                        _manualVeReconfigurationPending.Remove(machineNumber);
                        machine.ClearRecoveredOrder();
                        _startupRecoveryMachines.Remove(machineNumber);
                        continue;
                    }

                    var pendingHint = checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation
                        ? " PendingActivation darf nicht verworfen werden, weil die LOGO! nicht eindeutig leer/inaktiv ist."
                        : string.Empty;
                    throw new InvalidOperationException($"JobIdEcho {snapshot.JobIdEcho} != Recovery-JobId {checkpoint.JobId}.{pendingHint}");
                }

                if (snapshot.ActiveCavitiesEcho != checkpoint.ActiveCavities)
                    throw new InvalidOperationException($"Kavitäten-Echo {snapshot.ActiveCavitiesEcho} != Soll {checkpoint.ActiveCavities}.");

                // Freeze the known job before reconstructing any PC-side state.
                await _fleet.PauseCountingAsync(machineNumber);
                snapshot = await _fleet.ReadSnapshotAsync(machineNumber);
                if (snapshot.JobIdEcho != checkpoint.JobId || snapshot.ActiveCavitiesEcho != checkpoint.ActiveCavities)
                    throw new InvalidOperationException("Auftragsidentität änderte sich während des Recovery-Abgleichs.");

                machine.RestoreRecoveredOrder(checkpoint with { Phase = ActiveOrderCheckpointPhase.Active }, snapshot);

                var producedBeforeCurrentVe = machine.OrderProducedQuantity >= machine.CurrentParts
                    ? machine.OrderProducedQuantity - machine.CurrentParts
                    : 0;
                VeBoundaryPlan? currentPlan = null;
                if (machine.OrderState != ProductionOrderState.Completed)
                    currentPlan = VeBoundaryPolicy.Plan(machine.CurrentVeNumber, producedBeforeCurrentVe,
                        machine.OrderTargetQuantity, machine.TargetPartsPerVe, machine.ActiveCavities);

                var holdEcho = snapshot.HoldAfterVeNumberEcho;
                var checkpointHold = checkpoint.ScheduledHoldAfterVeNumber;
                var planHold = currentPlan?.HoldAfterVeNumber ?? checkpointHold;
                if (holdEcho == 0 || (holdEcho != checkpointHold && holdEcho != planHold))
                    throw new InvalidOperationException($"HoldAfterVE-Echo {holdEcho} passt weder zu Checkpoint {checkpointHold} noch zur aktuellen Planung {planHold}.");

                _scheduledCompletionHolds[machineNumber] = holdEcho;

                if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes)
                {
                    await _database.AddEventAsync(machineNumber, "RECOVERY_OFFLINE_PROGRESS",
                        $"Während Partcounter offline war, stieg CompletedVEs von {checkpoint.LastKnownCompletedVes} auf {snapshot.CompletedVes}. Keine künstlichen VE-Zeitstempel erzeugt; LOGO-Zählerstand wurde als Quelle übernommen.");
                }

                if (checkpoint.ManualVeReconfigurationPending)
                {
                    if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes && snapshot.LastCompletionReason == VeCompletionReason.Manual)
                    {
                        if (machine.OrderState != ProductionOrderState.Completed && currentPlan is not null)
                        {
                            var recoveryJob = new JobParameters(checkpoint.JobId, machine.ArticleNumber, machine.ToolNumber,
                                machine.ActiveCavities, currentPlan.TargetParts, currentPlan.TargetCycles, ValvePulseMs,
                                currentPlan.HoldAfterVeNumber);
                            await _fleet.UpdateVeTargetAsync(machineNumber, recoveryJob, pauseCounting: true);
                            _scheduledCompletionHolds[machineNumber] = currentPlan.HoldAfterVeNumber;
                        }
                        _manualVeReconfigurationPending.Remove(machineNumber);
                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_RESOLVED",
                            $"Manueller VE-Wechsel wurde während des Neustarts eindeutig über CompletedVEs/LastCompletionReason erkannt und sicher neu geplant.");
                    }
                    else
                    {
                        _manualVeReconfigurationPending.Add(machineNumber);
                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_STILL_PENDING",
                            "Manueller VE-Wechsel ist nicht eindeutig als abgeschlossen erkennbar. Normaler Resume bleibt gesperrt; Reset/Abbruch oder eindeutiger Completion-Nachweis erforderlich.");
                    }
                }

                if (machine.OrderState == ProductionOrderState.Completed)
                {
                    if (snapshot.CurrentVeNumber > holdEcho &&
                        (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)
                        throw new InvalidOperationException("Auftrag ist laut Zähler vollständig, aber der erwartete Completion-Hold ist nicht aktiv.");

                    _scheduledCompletionHolds.Remove(machineNumber);
                    _manualVeReconfigurationPending.Remove(machineNumber);
                    await DeleteLiveOrderCheckpointAsync(machineNumber);
                    await _database.AddEventAsync(machineNumber, "RECOVERY_JOB_COMPLETED",
                        $"Auftrag {machine.OrderNumber} war beim Wiederanlauf bereits vollständig. LOGO-Zählung wurde bestätigt pausiert; fehlende Offline-VE-Zeitstempel wurden nicht erfunden.");
                    _startupRecoveryMachines.Remove(machineNumber);
                    continue;
                }

                await PersistLiveOrderCheckpointAsync(machine);
                await _database.AddEventAsync(machineNumber, "RECOVERY_JOB_VERIFIED_PAUSED",
                    $"Auftrag {machine.OrderNumber}: JobId {checkpoint.JobId}, Kavitäten {checkpoint.ActiveCavities}, Hold {holdEcho} bestätigt. Auftrag bleibt absichtlich pausiert.");
            }
            catch (Exception ex)
            {
                if (machine.OrderState == ProductionOrderState.Running)
                    machine.PauseOrder();
                errors.Add($"M{machineNumber:00}: {ex.Message}");
                await _database.AddEventAsync(machineNumber, "RECOVERY_FAILED", ex.Message);
            }
        }

        return errors;
    }

    private async Task PersistPendingActivationAsync(
        MachineState machine,
        ArticleDefinition article,
        string orderNumber,
        uint orderTargetQuantity,
        VeBoundaryPlan firstPlan,
        uint jobId)
    {
        var checkpoint = new ActiveOrderCheckpoint(
            machine.Configuration.MachineNumber,
            orderNumber,
            jobId,
            article.ArticleNumber,
            article.Description,
            article.ToolNumber,
            article.ActiveCavities,
            article.PackagingQuantity,
            orderTargetQuantity,
            ProductionOrderState.Paused,
            firstPlan.HoldAfterVeNumber,
            false,
            false,
            0, 0, 0, 1, 0, 0,
            ActiveOrderCheckpointPhase.PendingActivation,
            DateTime.UtcNow);
        await OrderRecovery.UpsertAsync(checkpoint);
        _liveOrderCheckpoints[machine.Configuration.MachineNumber] = checkpoint;
    }

    private async Task PersistLiveOrderCheckpointAsync(MachineState machine)
    {
        // Never persist ordinary simulation orders. Existing startup-recovery records are
        // allowed while IsSimulationMode is true because they represent real production.
        var machineNumber = machine.Configuration.MachineNumber;
        if (IsSimulationMode && !_liveOrderCheckpoints.ContainsKey(machineNumber))
            return;
        if (string.IsNullOrWhiteSpace(machine.OrderNumber) || machine.OrderTargetQuantity == 0)
            return;

        var hold = _scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduled) ? scheduled : (ushort)0;
        var checkpoint = new ActiveOrderCheckpoint(
            machineNumber,
            machine.OrderNumber,
            GetActiveJobId(machine),
            machine.ArticleNumber,
            machine.ArticleDescription,
            machine.ToolNumber,
            machine.ActiveCavities,
            machine.TargetPartsPerVe,
            machine.OrderTargetQuantity,
            machine.OrderState,
            hold,
            _manualVeReconfigurationPending.Contains(machineNumber),
            machine.IsTemporarilyDisabled,
            machine.OrderProducedQuantity,
            machine.CurrentParts,
            machine.TotalCycles,
            machine.CurrentVeNumber,
            machine.CompletedVes,
            machine.LastCompletedVeQuantity,
            ActiveOrderCheckpointPhase.Active,
            DateTime.UtcNow);

        await OrderRecovery.UpsertAsync(checkpoint);
        _liveOrderCheckpoints[machineNumber] = checkpoint;
    }

    private async Task DeleteLiveOrderCheckpointAsync(int machineNumber)
    {
        await OrderRecovery.InitializeAsync();
        await OrderRecovery.DeleteAsync(machineNumber);
        _liveOrderCheckpoints.Remove(machineNumber);
        _activeJobIds.Remove(machineNumber);
    }

    private uint GetActiveJobId(MachineState machine)
    {
        var machineNumber = machine.Configuration.MachineNumber;
        if (_activeJobIds.TryGetValue(machineNumber, out var jobId) && jobId != 0)
            return jobId;
        if (_liveOrderCheckpoints.TryGetValue(machineNumber, out var checkpoint) && checkpoint.JobId != 0)
        {
            _activeJobIds[machineNumber] = checkpoint.JobId;
            return checkpoint.JobId;
        }
        throw new InvalidOperationException($"{machine.DisplayName}: keine eindeutige aktive JobId vorhanden; Modbus-Auftragsänderung wird gesperrt.");
    }
}
