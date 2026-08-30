from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8", newline="\n")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"Pattern not found for {label}")
    return text.replace(old, new, 1)


# ---------------------------------------------------------------------------
# 1) Pure recovery boundary policy: testable decision before any Modbus release.
# ---------------------------------------------------------------------------
write(
    "src/Partcounter.App/Services/RecoveryBoundaryPolicy.cs",
    '''using Partcounter.Models;\n\nnamespace Partcounter.Services;\n\npublic enum RecoveryBoundaryAction\n{\n    ContinuePaused = 0,\n    ReconfigureHeldBoundary = 1,\n    FinalHeldBoundary = 2,\n    Reject = 3\n}\n\npublic sealed record RecoveryBoundaryDecision(RecoveryBoundaryAction Action, string? Error = null);\n\npublic static class RecoveryBoundaryPolicy\n{\n    public static RecoveryBoundaryDecision Decide(\n        LogoSnapshot snapshot,\n        ushort checkpointHold,\n        ushort plannedHold,\n        bool orderCompleted)\n    {\n        var holdEcho = snapshot.HoldAfterVeNumberEcho;\n        if (holdEcho == 0)\n            return Reject("LOGO! meldet keinen HoldAfterVE-Echo.");\n\n        if (holdEcho != checkpointHold && holdEcho != plannedHold)\n            return Reject($"HoldAfterVE-Echo {holdEcho} passt weder zu Checkpoint {checkpointHold} noch zur aktuellen Planung {plannedHold}.");\n\n        var holdActive = (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0;\n        if (snapshot.CompletedVes > holdEcho)\n            return Reject($"LOGO! hat den geplanten Grenzhalt VE {holdEcho} überschritten (CompletedVEs={snapshot.CompletedVes}).");\n\n        if (snapshot.CompletedVes == holdEcho)\n        {\n            if (!holdActive)\n                return Reject($"VE {holdEcho} ist abgeschlossen, aber CompletionHoldActive ist nicht gesetzt.");\n            return new RecoveryBoundaryDecision(orderCompleted\n                ? RecoveryBoundaryAction.FinalHeldBoundary\n                : RecoveryBoundaryAction.ReconfigureHeldBoundary);\n        }\n\n        if (holdActive)\n            return Reject($"CompletionHoldActive ist vor der geplanten Grenze VE {holdEcho} aktiv (CompletedVEs={snapshot.CompletedVes}).");\n\n        if (orderCompleted)\n            return Reject("PC-Auftragsmenge ist vollständig, obwohl die geplante LOGO!-Hold-Grenze noch nicht erreicht wurde.");\n\n        return new RecoveryBoundaryDecision(RecoveryBoundaryAction.ContinuePaused);\n    }\n\n    private static RecoveryBoundaryDecision Reject(string message) =>\n        new(RecoveryBoundaryAction.Reject, message);\n}\n''',
)

# ---------------------------------------------------------------------------
# 2) MachineFleet: polling may run, but snapshots are not published to normal UI/event
#    processing until recovery identity/boundary reconciliation has completed.
# ---------------------------------------------------------------------------
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
text = replace_once(
    text,
    "    public async Task StartAsync(IEnumerable<MachineConfiguration> configurations)\n",
    "    public async Task StartAsync(IEnumerable<MachineConfiguration> configurations, bool publishSnapshots = true)\n",
    "StartAsync signature",
)
text = replace_once(
    text,
    "            var session = new Session(configuration);",
    "            var session = new Session(configuration, publishSnapshots);",
    "Session constructor call",
)
text = replace_once(
    text,
    "            UpdateGlobalDiagnostics(session);\n            SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(machineNumber, snapshot));\n            PublishConnection(session, ConnectionState.Online, null);",
    "            UpdateGlobalDiagnostics(session);\n            PublishConnection(session, ConnectionState.Online, null);",
    "ReadSnapshot no event publication",
)
insert_publish_method = '''\n    public async Task SetSnapshotPublishingEnabledAsync(\n        int machineNumber,\n        bool enabled,\n        CancellationToken cancellationToken = default)\n    {\n        var session = GetSession(machineNumber);\n        await session.Gate.WaitAsync(cancellationToken);\n        try\n        {\n            session.SnapshotPublishingEnabled = enabled;\n            UpdateGlobalDiagnostics(session);\n        }\n        finally\n        {\n            session.Gate.Release();\n        }\n    }\n'''
text = replace_once(
    text,
    "\n    public async Task SendJobAsync(int machineNumber, JobParameters job, CancellationToken cancellationToken = default)\n",
    insert_publish_method + "\n    public async Task SendJobAsync(int machineNumber, JobParameters job, CancellationToken cancellationToken = default)\n",
    "snapshot publishing API",
)
text = replace_once(
    text,
    "                    UpdateGlobalDiagnostics(session);\n                    SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));\n                    PublishConnection(session, ConnectionState.Online, null);",
    "                    UpdateGlobalDiagnostics(session);\n                    if (session.SnapshotPublishingEnabled)\n                        SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));\n                    PublishConnection(session, ConnectionState.Online, null);",
    "PollLoop publication guard",
)
text = replace_once(
    text,
    "        UpdateGlobalDiagnostics(session);\n        SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));\n    }",
    "        UpdateGlobalDiagnostics(session);\n        if (session.SnapshotPublishingEnabled)\n            SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));\n    }",
    "EnsureCommandSequence publication guard",
)
text = replace_once(
    text,
    "        public Session(MachineConfiguration configuration)\n        {\n            Configuration = configuration;\n            Client = new LogoModbusClient(configuration);\n        }",
    "        public Session(MachineConfiguration configuration, bool snapshotPublishingEnabled)\n        {\n            Configuration = configuration;\n            Client = new LogoModbusClient(configuration);\n            SnapshotPublishingEnabled = snapshotPublishingEnabled;\n        }",
    "Session constructor",
)
text = replace_once(
    text,
    "        public bool PollingEnabled { get; set; } = true;\n",
    "        public bool PollingEnabled { get; set; } = true;\n        public bool SnapshotPublishingEnabled { get; set; }\n",
    "Session publish property",
)
write(path, text)

# ---------------------------------------------------------------------------
# 3) Main operating-mode transition: suppress ALL normal snapshot events while the
#    fleet is being preflighted/recovered. Publish only after IsSimulationMode=false.
# ---------------------------------------------------------------------------
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
text = replace_once(
    text,
    "            await _fleet.StartAsync(Machines.Select(m => m.Configuration));",
    "            await _fleet.StartAsync(Machines.Select(m => m.Configuration), publishSnapshots: false);",
    "suppressed fleet start",
)
old_after_recovery = '''            foreach (var machine in Machines.Where(m => m.IsTemporarilyDisabled))\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n\n            var recoveredCount = _startupRecoveryMachines.Count;\n            _startupRecoveryMachines.Clear();\n            IsSimulationMode = false;\n            StatusMessage = recoveredCount > 0\n'''
new_after_recovery = '''            var recoveredCount = _startupRecoveryMachines.Count;\n            _startupRecoveryMachines.Clear();\n            IsSimulationMode = false;\n\n            foreach (var machine in Machines)\n            {\n                if (machine.IsTemporarilyDisabled)\n                {\n                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n                    await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n                }\n                else\n                {\n                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n                }\n            }\n\n            StatusMessage = recoveredCount > 0\n'''
text = replace_once(text, old_after_recovery, new_after_recovery, "enable publishing after mode transition")

# Re-enable publishing when an intentionally disabled live machine is explicitly enabled.
old_toggle_poll = '''                await _fleet.SetMachinePollingEnabledAsync(\n                    machine.Configuration.MachineNumber,\n                    enabled: !disable);\n            }\n\n            machine.SetTemporarilyDisabled(disable);'''
new_toggle_poll = '''                if (disable)\n                {\n                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n                    await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n                }\n                else\n                {\n                    await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n                }\n            }\n\n            machine.SetTemporarilyDisabled(disable);'''
text = replace_once(text, old_toggle_poll, new_toggle_poll, "temporary machine snapshot/poll coordination")

# A job can revive a temporarily disabled machine; keep snapshots suppressed until the
# command, JobId/Hold echo and local StartOrder are all established.
text = replace_once(
    text,
    "            if (wasTemporarilyDisabled)\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n\n            await PersistPendingActivationAsync",
    "            if (wasTemporarilyDisabled)\n            {\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n                await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\n            }\n\n            await PersistPendingActivationAsync",
    "disabled machine job start suppression",
)
text = replace_once(
    text,
    "        machine.StartOrder(article, order, OrderTargetQuantity);\n        _scheduledCompletionHolds[machine.Configuration.MachineNumber] = firstPlan.HoldAfterVeNumber;",
    "        machine.StartOrder(article, order, OrderTargetQuantity);\n        _scheduledCompletionHolds[machine.Configuration.MachineNumber] = firstPlan.HoldAfterVeNumber;\n        if (!IsSimulationMode && wasTemporarilyDisabled)\n            await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);",
    "publish after local StartOrder",
)

# Every target update that is supposed to release a held boundary must prove that the
# LOGO has actually cleared CompletionHoldActive before Resume is allowed.
text = replace_once(
    text,
    "            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);\n            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;\n            _manualVeReconfigurationPending.Remove(machineNumber);\n            if (machine.OrderState == ProductionOrderState.Running)",
    "            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);\n            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;\n            await ConfirmCompletionHoldReleasedAsync(machineNumber, nextJob.JobId, machine.ActiveCavities, nextPlan.HoldAfterVeNumber);\n            _manualVeReconfigurationPending.Remove(machineNumber);\n            if (machine.OrderState == ProductionOrderState.Running)",
    "manual VE hold release confirmation",
)
text = replace_once(
    text,
    "            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);\n            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;\n            if (machine.OrderState == ProductionOrderState.Running)",
    "            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);\n            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;\n            await ConfirmCompletionHoldReleasedAsync(machineNumber, nextJob.JobId, machine.ActiveCavities, nextPlan.HoldAfterVeNumber);\n            if (machine.OrderState == ProductionOrderState.Running)",
    "automatic VE hold release confirmation",
)

# New/reset job must also clear any stale old CompletionHoldActive before local counting state is released.
text = replace_once(
    text,
    "                await _fleet.SendJobAsync(machine.Configuration.MachineNumber, job);\n            }\n            catch (Exception ex)",
    "                await _fleet.SendJobAsync(machine.Configuration.MachineNumber, job);\n                await ConfirmCompletionHoldReleasedAsync(machine.Configuration.MachineNumber, job.JobId, article.ActiveCavities, firstPlan.HoldAfterVeNumber);\n            }\n            catch (Exception ex)",
    "new job hold release confirmation",
)
text = replace_once(
    text,
    "            await _fleet.SendJobAsync(machine.Configuration.MachineNumber, resetJob);\n            machine.ResetCounters();",
    "            await _fleet.SendJobAsync(machine.Configuration.MachineNumber, resetJob);\n            await ConfirmCompletionHoldReleasedAsync(machine.Configuration.MachineNumber, resetJob.JobId, machine.ActiveCavities, resetPlan.HoldAfterVeNumber);\n            machine.ResetCounters();",
    "reset hold release confirmation",
)
write(path, text)

# ---------------------------------------------------------------------------
# 4) Recovery reconciliation: resolve uncertain manual completion first, then make a
#    formal boundary decision. If LOGO is already held, reconfigure under pause and
#    verify HoldActive is gone before leaving recovery.
# ---------------------------------------------------------------------------
path = "src/Partcounter.App/ViewModels/MainViewModel.Recovery.cs"
text = read(path)
# Do not turn polling on: StartAsync already polls with publication suppressed, and direct
# reads work regardless of normal snapshot publication.
text = replace_once(
    text,
    "                await _fleet.SetMachinePollingEnabledAsync(machineNumber, enabled: true);\n                var snapshot = await _fleet.ReadSnapshotAsync(machineNumber);",
    "                var snapshot = await _fleet.ReadSnapshotAsync(machineNumber);",
    "recovery direct read without publish enable",
)

old_hold_and_manual = '''                var holdEcho = snapshot.HoldAfterVeNumberEcho;\n                var checkpointHold = checkpoint.ScheduledHoldAfterVeNumber;\n                var planHold = currentPlan?.HoldAfterVeNumber ?? checkpointHold;\n                if (holdEcho == 0 || (holdEcho != checkpointHold && holdEcho != planHold))\n                    throw new InvalidOperationException($"HoldAfterVE-Echo {holdEcho} passt weder zu Checkpoint {checkpointHold} noch zur aktuellen Planung {planHold}.");\n\n                _scheduledCompletionHolds[machineNumber] = holdEcho;\n\n                if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes)\n                {\n                    await _database.AddEventAsync(machineNumber, "RECOVERY_OFFLINE_PROGRESS",\n                        $"Während Partcounter offline war, stieg CompletedVEs von {checkpoint.LastKnownCompletedVes} auf {snapshot.CompletedVes}. Keine künstlichen VE-Zeitstempel erzeugt; LOGO-Zählerstand wurde als Quelle übernommen.");\n                }\n\n                if (checkpoint.ManualVeReconfigurationPending)\n                {\n                    if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes && snapshot.LastCompletionReason == VeCompletionReason.Manual)\n                    {\n                        if (machine.OrderState != ProductionOrderState.Completed && currentPlan is not null)\n                        {\n                            var recoveryJob = new JobParameters(checkpoint.JobId, machine.ArticleNumber, machine.ToolNumber,\n                                machine.ActiveCavities, currentPlan.TargetParts, currentPlan.TargetCycles, ValvePulseMs,\n                                currentPlan.HoldAfterVeNumber);\n                            await _fleet.UpdateVeTargetAsync(machineNumber, recoveryJob, pauseCounting: true);\n                            _scheduledCompletionHolds[machineNumber] = currentPlan.HoldAfterVeNumber;\n                        }\n                        _manualVeReconfigurationPending.Remove(machineNumber);\n                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_RESOLVED",\n                            $"Manueller VE-Wechsel wurde während des Neustarts eindeutig über CompletedVEs/LastCompletionReason erkannt und sicher neu geplant.");\n                    }\n                    else\n                    {\n                        _manualVeReconfigurationPending.Add(machineNumber);\n                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_STILL_PENDING",\n                            "Manueller VE-Wechsel ist nicht eindeutig als abgeschlossen erkennbar. Normaler Resume bleibt gesperrt; Reset/Abbruch oder eindeutiger Completion-Nachweis erforderlich.");\n                    }\n                }\n\n                if (machine.OrderState == ProductionOrderState.Completed)\n                {\n                    if (snapshot.CurrentVeNumber > holdEcho &&\n                        (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)\n                        throw new InvalidOperationException("Auftrag ist laut Zähler vollständig, aber der erwartete Completion-Hold ist nicht aktiv.");\n\n                    _scheduledCompletionHolds.Remove(machineNumber);\n                    _manualVeReconfigurationPending.Remove(machineNumber);\n                    await DeleteLiveOrderCheckpointAsync(machineNumber);\n                    await _database.AddEventAsync(machineNumber, "RECOVERY_JOB_COMPLETED",\n                        $"Auftrag {machine.OrderNumber} war beim Wiederanlauf bereits vollständig. LOGO-Zählung wurde bestätigt pausiert; fehlende Offline-VE-Zeitstempel wurden nicht erfunden.");\n                    _startupRecoveryMachines.Remove(machineNumber);\n                    continue;\n                }\n'''
new_hold_and_manual = '''                var checkpointHold = checkpoint.ScheduledHoldAfterVeNumber;\n                var planHold = currentPlan?.HoldAfterVeNumber ?? checkpointHold;\n\n                if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes)\n                {\n                    await _database.AddEventAsync(machineNumber, "RECOVERY_OFFLINE_PROGRESS",\n                        $"Während Partcounter offline war, stieg CompletedVEs von {checkpoint.LastKnownCompletedVes} auf {snapshot.CompletedVes}. Keine künstlichen VE-Zeitstempel erzeugt; LOGO-Zählerstand wurde als Quelle übernommen.");\n                }\n\n                if (checkpoint.ManualVeReconfigurationPending)\n                {\n                    if (snapshot.CompletedVes > checkpoint.LastKnownCompletedVes && snapshot.LastCompletionReason == VeCompletionReason.Manual)\n                    {\n                        if (machine.OrderState != ProductionOrderState.Completed && currentPlan is not null)\n                        {\n                            var recoveryJob = new JobParameters(checkpoint.JobId, machine.ArticleNumber, machine.ToolNumber,\n                                machine.ActiveCavities, currentPlan.TargetParts, currentPlan.TargetCycles, ValvePulseMs,\n                                currentPlan.HoldAfterVeNumber);\n                            await _fleet.UpdateVeTargetAsync(machineNumber, recoveryJob, pauseCounting: true);\n                            _scheduledCompletionHolds[machineNumber] = currentPlan.HoldAfterVeNumber;\n                            snapshot = await ConfirmCompletionHoldReleasedAsync(machineNumber, checkpoint.JobId, machine.ActiveCavities, currentPlan.HoldAfterVeNumber);\n                            planHold = currentPlan.HoldAfterVeNumber;\n                        }\n                        _manualVeReconfigurationPending.Remove(machineNumber);\n                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_RESOLVED",\n                            "Manueller VE-Wechsel wurde über CompletedVEs/LastCompletionReason eindeutig erkannt, unter bestätigter Pause neu geplant und der Completion-Hold ist gelöst.");\n                    }\n                    else\n                    {\n                        _manualVeReconfigurationPending.Add(machineNumber);\n                        await _database.AddEventAsync(machineNumber, "RECOVERY_MANUAL_VE_STILL_PENDING",\n                            "Manueller VE-Wechsel ist nicht eindeutig als abgeschlossen erkennbar. Normaler Resume bleibt gesperrt; Reset/Abbruch oder eindeutiger Completion-Nachweis erforderlich.");\n                    }\n                }\n\n                var boundaryDecision = RecoveryBoundaryPolicy.Decide(\n                    snapshot, checkpointHold, planHold, machine.OrderState == ProductionOrderState.Completed);\n                if (boundaryDecision.Action == RecoveryBoundaryAction.Reject)\n                    throw new InvalidOperationException(boundaryDecision.Error ?? "Ungültiger LOGO!-Recovery-Grenzzustand.");\n\n                if (boundaryDecision.Action == RecoveryBoundaryAction.ReconfigureHeldBoundary)\n                {\n                    if (currentPlan is null)\n                        throw new InvalidOperationException("LOGO! steht am Completion-Hold, aber es existiert kein nächster VE-Plan.");\n\n                    var recoveryJob = new JobParameters(checkpoint.JobId, machine.ArticleNumber, machine.ToolNumber,\n                        machine.ActiveCavities, currentPlan.TargetParts, currentPlan.TargetCycles, ValvePulseMs,\n                        currentPlan.HoldAfterVeNumber);\n                    await _fleet.UpdateVeTargetAsync(machineNumber, recoveryJob, pauseCounting: true);\n                    _scheduledCompletionHolds[machineNumber] = currentPlan.HoldAfterVeNumber;\n                    snapshot = await ConfirmCompletionHoldReleasedAsync(machineNumber, checkpoint.JobId, machine.ActiveCavities, currentPlan.HoldAfterVeNumber);\n                    await _database.AddEventAsync(machineNumber, "RECOVERY_BOUNDARY_RECONFIGURED",\n                        $"Offline erreichter Grenzhalt VE {checkpointHold} unter Pause neu konfiguriert: Ziel {currentPlan.TargetParts} Teile, nächster Hold VE {currentPlan.HoldAfterVeNumber}. HoldActive ist bestätigt gelöst.");\n                }\n                else\n                {\n                    _scheduledCompletionHolds[machineNumber] = snapshot.HoldAfterVeNumberEcho;\n                }\n\n                if (boundaryDecision.Action == RecoveryBoundaryAction.FinalHeldBoundary)\n                {\n                    _scheduledCompletionHolds.Remove(machineNumber);\n                    _manualVeReconfigurationPending.Remove(machineNumber);\n                    await DeleteLiveOrderCheckpointAsync(machineNumber);\n                    await _database.AddEventAsync(machineNumber, "RECOVERY_JOB_COMPLETED",\n                        $"Auftrag {machine.OrderNumber} war beim Wiederanlauf bereits vollständig und steht am bestätigten finalen Completion-Hold. Fehlende Offline-VE-Zeitstempel wurden nicht erfunden.");\n                    _startupRecoveryMachines.Remove(machineNumber);\n                    continue;\n                }\n'''
text = replace_once(text, old_hold_and_manual, new_hold_and_manual, "recovery boundary/manual decision")

# Shared proof used by normal VE boundary, manual VE, new/reset order and recovery.
confirm_method = '''\n    private async Task<LogoSnapshot> ConfirmCompletionHoldReleasedAsync(\n        int machineNumber,\n        uint expectedJobId,\n        ushort expectedCavities,\n        ushort expectedHoldAfterVeNumber)\n    {\n        var snapshot = await _fleet.ReadSnapshotAsync(machineNumber);\n        if (snapshot.JobIdEcho != expectedJobId)\n            throw new InvalidOperationException($"JobIdEcho {snapshot.JobIdEcho} entspricht nach Rekonfiguration nicht Soll {expectedJobId}.");\n        if (snapshot.ActiveCavitiesEcho != expectedCavities)\n            throw new InvalidOperationException($"Kavitäten-Echo {snapshot.ActiveCavitiesEcho} entspricht nach Rekonfiguration nicht Soll {expectedCavities}.");\n        if (snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber)\n            throw new InvalidOperationException($"HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nach Rekonfiguration nicht Soll {expectedHoldAfterVeNumber}.");\n        if ((snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) == 0)\n            throw new InvalidOperationException("CompletionHoldArmed ist nach Rekonfiguration nicht aktiv.");\n        if ((snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0)\n            throw new InvalidOperationException("CompletionHoldActive wurde durch die bestätigte Rekonfiguration nicht gelöst.");\n        return snapshot;\n    }\n'''
text = replace_once(
    text,
    "\n    private async Task PersistPendingActivationAsync(\n",
    confirm_method + "\n    private async Task PersistPendingActivationAsync(\n",
    "insert hold release proof",
)
write(path, text)

# ---------------------------------------------------------------------------
# 5) Tests for the recovery boundary decision.
# ---------------------------------------------------------------------------
path = "tests/Partcounter.Tests/ActiveOrderRecoveryTests.cs"
text = read(path)
tests = '''\n    [Fact]\n    public void RecoveryBoundary_RequiresReconfigurationAtHeldBoundary()\n    {\n        var snapshot = Snapshot(jobId: 1, totalCycles: 100, currentParts: 0, completedVes: 10,\n            statusWord: ModbusRegisterMap.StatusCompletionHoldActive | ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);\n        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 10, plannedHold: 11, orderCompleted: false);\n        Assert.Equal(RecoveryBoundaryAction.ReconfigureHeldBoundary, decision.Action);\n    }\n\n    [Fact]\n    public void RecoveryBoundary_RejectsPassedHoldWithoutLocalStop()\n    {\n        var snapshot = Snapshot(jobId: 1, totalCycles: 110, currentParts: 0, completedVes: 11,\n            statusWord: ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);\n        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 10, plannedHold: 12, orderCompleted: false);\n        Assert.Equal(RecoveryBoundaryAction.Reject, decision.Action);\n        Assert.Contains("überschritten", decision.Error);\n    }\n\n    [Fact]\n    public void RecoveryBoundary_FinalOrderRequiresActiveFinalHold()\n    {\n        var held = Snapshot(jobId: 1, totalCycles: 100, currentParts: 0, completedVes: 10,\n            statusWord: ModbusRegisterMap.StatusCompletionHoldActive | ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);\n        Assert.Equal(RecoveryBoundaryAction.FinalHeldBoundary,\n            RecoveryBoundaryPolicy.Decide(held, 10, 10, orderCompleted: true).Action);\n\n        var notHeld = held with { StatusWord = ModbusRegisterMap.StatusCompletionHoldArmed };\n        Assert.Equal(RecoveryBoundaryAction.Reject,\n            RecoveryBoundaryPolicy.Decide(notHeld, 10, 10, orderCompleted: true).Action);\n    }\n\n    [Fact]\n    public void RecoveryBoundary_AllowsUpdatedHoldEchoMatchingCurrentPlan()\n    {\n        var snapshot = Snapshot(jobId: 1, totalCycles: 50, currentParts: 128, completedVes: 5,\n            statusWord: ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 8);\n        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 7, plannedHold: 8, orderCompleted: false);\n        Assert.Equal(RecoveryBoundaryAction.ContinuePaused, decision.Action);\n    }\n'''
text = replace_once(
    text,
    "\n    private static LogoSnapshot Snapshot(uint jobId, uint totalCycles, uint currentParts, ushort completedVes, ushort statusWord) => new(\n",
    tests + "\n    private static LogoSnapshot Snapshot(uint jobId, uint totalCycles, uint currentParts, ushort completedVes, ushort statusWord, ushort holdAfterVe = 0) => new(\n",
    "insert recovery boundary tests",
)
text = replace_once(
    text,
    "        HoldAfterVeNumberEcho: 0,\n        JobIdEcho: jobId);",
    "        HoldAfterVeNumberEcho: holdAfterVe,\n        JobIdEcho: jobId);",
    "snapshot test helper hold",
)
write(path, text)

# ---------------------------------------------------------------------------
# 6) Engineering note.
# ---------------------------------------------------------------------------
path = "docs/RESTART_RECOVERY_R001_25.md"
text = read(path)
text += '''\n\n## Snapshot-Isolation während Recovery\nBeim Umschalten in Echtbetrieb startet die Kommunikationsflotte zunächst mit deaktivierter `SnapshotReceived`-Publikation. Polling/Heartbeat darf laufen, aber kein normaler MachineState-/VE-Eventpfad wird aktiviert, bevor JobId, Kavitäten und Grenzzustand abgeglichen sind. Direkte Recovery-Snapshots werden ebenfalls nicht in den normalen Eventpfad publiziert. Erst nach erfolgreichem Abgleich wird `IsSimulationMode=false` gesetzt und die Snapshot-Publikation freigegeben.\n\n## Offline erreichter Completion-Hold\nSteht die LOGO! beim Wiederanlauf bereits exakt an `HoldAfterVE`, muss `CompletionHoldActive` gesetzt sein. Partcounter schreibt unter bestätigter Pause zuerst das nächste VE-Ziel und den nächsten Hold, liest anschließend einen frischen Snapshot und verlangt:\n- JobIdEcho unverändert,\n- Kavitäten-Echo unverändert,\n- neues HoldAfterVE-Echo korrekt,\n- CompletionHoldArmed aktiv,\n- CompletionHoldActive **gelöst**.\n\nErst dann darf der Auftrag später durch den Bediener fortgesetzt werden. Ein überschrittener Hold oder ein abgeschlossener Hold ohne `CompletionHoldActive` blockiert den Echtbetrieb. Dieselbe Hold-Lösebestätigung wird auch bei normalen VE-Grenzen, manuellen VE-Neuplanungen sowie neuem/resettem Auftrag verwendet.\n'''
write(path, text)

print("R001.25 recovery hold/snapshot isolation hardening applied")
