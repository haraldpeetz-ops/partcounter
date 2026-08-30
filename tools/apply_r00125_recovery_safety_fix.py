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


write(
    "src/Partcounter.App/Services/RecoveryIdentityPolicy.cs",
    '''using Partcounter.Models;\n\nnamespace Partcounter.Services;\n\npublic static class RecoveryIdentityPolicy\n{\n    public static bool IsProvablyIdleForPendingActivation(LogoSnapshot snapshot) =>\n        snapshot.JobIdEcho == 0 &&\n        snapshot.TotalCycles == 0 &&\n        snapshot.CurrentParts == 0 &&\n        snapshot.CompletedVes == 0 &&\n        (snapshot.StatusWord & ModbusRegisterMap.StatusAutomaticEnabled) == 0;\n}\n''',
)

path = "src/Partcounter.App/ViewModels/MainViewModel.Recovery.cs"
text = read(path)
text = replace_once(
    text,
    "    private bool IsPendingStartupRecovery(MachineState machine) =>\n        IsSimulationMode && _startupRecoveryMachines.Contains(machine.Configuration.MachineNumber);\n",
    "    private bool IsPendingStartupRecovery(MachineState machine) =>\n        IsSimulationMode && _startupRecoveryMachines.Contains(machine.Configuration.MachineNumber);\n\n    private bool HasUnresolvedPendingActivation(MachineState machine) =>\n        _liveOrderCheckpoints.TryGetValue(machine.Configuration.MachineNumber, out var checkpoint) &&\n        checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation;\n",
    "pending activation helper",
)
text = replace_once(
    text,
    "                    if (checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation)\n                    {\n                        // Crash before a verifiable LOGO activation: no matching job exists.\n                        await _database.AddEventAsync(machineNumber, \"RECOVERY_PENDING_START_NOT_ACTIVE\",\n                            $\"Pending-Auftrag {checkpoint.OrderNumber} (JobId {checkpoint.JobId}) ist in der LOGO! nicht aktiv; Checkpoint wird verworfen, ohne einen fremden Auftrag zu verändern.\");\n                        await DeleteLiveOrderCheckpointAsync(machineNumber);\n                        _scheduledCompletionHolds.Remove(machineNumber);\n                        _manualVeReconfigurationPending.Remove(machineNumber);\n                        machine.ClearRecoveredOrder();\n                        _startupRecoveryMachines.Remove(machineNumber);\n                        continue;\n                    }\n\n                    throw new InvalidOperationException($\"JobIdEcho {snapshot.JobIdEcho} != Recovery-JobId {checkpoint.JobId}.\");",
    "                    if (checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation &&\n                        RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(snapshot))\n                    {\n                        // The pending PC command is definitely not active and the LOGO reports no\n                        // autonomous production state. Only in this proven-idle case may we discard it.\n                        await _database.AddEventAsync(machineNumber, \"RECOVERY_PENDING_START_NOT_ACTIVE\",\n                            $\"Pending-Auftrag {checkpoint.OrderNumber} (JobId {checkpoint.JobId}) wurde nicht aktiv; LOGO! ist nachweislich leer/inaktiv. Checkpoint wird verworfen.\");\n                        await DeleteLiveOrderCheckpointAsync(machineNumber);\n                        _scheduledCompletionHolds.Remove(machineNumber);\n                        _manualVeReconfigurationPending.Remove(machineNumber);\n                        machine.ClearRecoveredOrder();\n                        _startupRecoveryMachines.Remove(machineNumber);\n                        continue;\n                    }\n\n                    var pendingHint = checkpoint.Phase == ActiveOrderCheckpointPhase.PendingActivation\n                        ? \" PendingActivation darf nicht verworfen werden, weil die LOGO! nicht eindeutig leer/inaktiv ist.\"\n                        : string.Empty;\n                    throw new InvalidOperationException($\"JobIdEcho {snapshot.JobIdEcho} != Recovery-JobId {checkpoint.JobId}.{pendingHint}\");",
    "safe pending mismatch",
)
write(path, text)

path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
text = replace_once(
    text,
    "        if (SelectedMachine.IsActiveOrder)\n        {\n            StatusMessage = $\"{SelectedMachine.DisplayName}: Es läuft bereits ein Auftrag. Bitte zuerst pausieren/beenden.\";\n            return;\n        }",
    "        if (HasUnresolvedPendingActivation(SelectedMachine))\n        {\n            StatusMessage = $\"{SelectedMachine.DisplayName}: Neue Beauftragung gesperrt. Ein vorheriger Echtauftrag ist wegen verlorener/fehlender Modbus-Bestätigung noch als PendingActivation offen. Nach Wiederherstellung der Verbindung Partcounter neu starten und den Recovery-Abgleich ausführen.\";\n            return;\n        }\n\n        if (SelectedMachine.IsActiveOrder)\n        {\n            StatusMessage = $\"{SelectedMachine.DisplayName}: Es läuft bereits ein Auftrag. Bitte zuerst pausieren/beenden.\";\n            return;\n        }",
    "block new order on pending activation",
)
text = replace_once(
    text,
    "                await DeleteLiveOrderCheckpointAsync(machine.Configuration.MachineNumber);\n                StatusMessage = $\"Auftrag nicht übernommen – LOGO!-Protocol-V3-Übertragung fehlgeschlagen: {ex.Message}\";\n                await _database.AddEventAsync(machine.Configuration.MachineNumber, \"MODBUS_WRITE_ERROR\", ex.Message);\n                return;",
    "                StatusMessage = $\"Auftrag nicht eindeutig übernommen – LOGO!-Protocol-V3-Bestätigung fehlgeschlagen: {ex.Message} PendingActivation bleibt gespeichert; auf dieser Maschine wird keine neue Beauftragung zugelassen, bis der Recovery-Abgleich den realen LOGO!-Zustand eindeutig geklärt hat.\";\n                await _database.AddEventAsync(machine.Configuration.MachineNumber, \"MODBUS_WRITE_UNCERTAIN\", StatusMessage);\n                return;",
    "preserve uncertain pending activation",
)
write(path, text)

path = "tests/Partcounter.Tests/ActiveOrderRecoveryTests.cs"
text = read(path)
insert = '''\n    [Fact]\n    public void PendingActivation_IsDiscardableOnlyForProvablyIdleLogo()\n    {\n        var idle = Snapshot(jobId: 0, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: 0);\n        Assert.True(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(idle));\n\n        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(\n            Snapshot(jobId: 999, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: 0)));\n        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(\n            Snapshot(jobId: 0, totalCycles: 1, currentParts: 64, completedVes: 0, statusWord: 0)));\n        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(\n            Snapshot(jobId: 0, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: ModbusRegisterMap.StatusAutomaticEnabled)));\n    }\n\n    private static LogoSnapshot Snapshot(uint jobId, uint totalCycles, uint currentParts, ushort completedVes, ushort statusWord) => new(\n        CurrentParts: currentParts,\n        TotalCycles: totalCycles,\n        CurrentVeNumber: 1,\n        CompletedVes: completedVes,\n        LastCompletedVeQuantity: 0,\n        StatusWord: statusWord,\n        AcknowledgedCommandSequence: 0,\n        ActiveCavitiesEcho: 1,\n        LastCompletedVeNumber: 0,\n        CompletionSequence: 0,\n        LogoHeartbeat: 1,\n        ErrorCode: 0,\n        LastCompletionReason: VeCompletionReason.Unknown,\n        ReadAtUtc: DateTime.UtcNow,\n        HoldAfterVeNumberEcho: 0,\n        JobIdEcho: jobId);\n'''
text = replace_once(
    text,
    "\n    private static ActiveOrderCheckpoint CreateCheckpoint() => new(",
    insert + "\n    private static ActiveOrderCheckpoint CreateCheckpoint() => new(",
    "recovery identity tests",
)
write(path, text)

path = "docs/RESTART_RECOVERY_R001_25.md"
text = read(path)
text += '''\n\n## Unklare Auftragsübernahme\nWenn ein realer Auftragswrite wegen Verbindungsabbruch nicht eindeutig bestätigt werden kann, bleibt `PendingActivation` absichtlich erhalten. Partcounter darf auf dieser Maschine keinen neuen Auftrag starten, weil die LOGO! den Write möglicherweise bereits verarbeitet hat. Der Recovery-Abgleich entscheidet später anhand von `JobIdEcho`.\n\nEin Pending-Checkpoint mit abweichender JobId wird nur dann automatisch verworfen, wenn die LOGO! **nachweislich leer/inaktiv** ist: JobIdEcho=0, TotalCycles=0, CurrentParts=0, CompletedVEs=0 und AutomaticEnabled=0. Jeder andere fremde oder unklare LOGO!-Zustand blockiert die Echtbetriebsaktivierung.\n'''
write(path, text)

print("R001.25 recovery safety fix applied")
