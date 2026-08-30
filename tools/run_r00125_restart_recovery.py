from pathlib import Path

# Run the staged recovery patch only after removing two misleading timestamp assignments.
# A recovery read proves counters and identity, but it does NOT prove when the last physical
# cycle or VE completion occurred. Those timestamps must therefore remain unknown.
patch = Path("tools/apply_r00125_restart_recovery.py")
text = patch.read_text(encoding="utf-8")
text = text.replace(
    '            LastCycleLocal = snapshot.TotalCycles > 0 ? snapshot.ReadAtUtc.ToLocalTime() : null;\\n            LastVeCompletedLocal = snapshot.CompletedVes > 0 ? snapshot.ReadAtUtc.ToLocalTime() : null;\\n',
    '            LastCycleLocal = null;\\n            LastVeCompletedLocal = null;\\n',
    1,
)

# When a PendingActivation checkpoint never became the active LOGO job, discard every
# local planning flag together with the checkpoint. Never let stale hold state survive.
text = text.replace(
    '                        await DeleteLiveOrderCheckpointAsync(machineNumber);\\n                        machine.ClearRecoveredOrder();\\n                        _startupRecoveryMachines.Remove(machineNumber);\\n                        continue;\\n',
    '                        await DeleteLiveOrderCheckpointAsync(machineNumber);\\n                        _scheduledCompletionHolds.Remove(machineNumber);\\n                        _manualVeReconfigurationPending.Remove(machineNumber);\\n                        machine.ClearRecoveredOrder();\\n                        _startupRecoveryMachines.Remove(machineNumber);\\n                        continue;\\n',
    1,
)

if 'LastCycleLocal = snapshot.TotalCycles > 0 ? snapshot.ReadAtUtc.ToLocalTime() : null;' in text:
    raise SystemExit("Recovery wrapper failed to remove fabricated cycle timestamp")
if 'LastVeCompletedLocal = snapshot.CompletedVes > 0 ? snapshot.ReadAtUtc.ToLocalTime() : null;' in text:
    raise SystemExit("Recovery wrapper failed to remove fabricated VE timestamp")

code = compile(text, str(patch), "exec")
exec(code, {"__name__": "__main__", "__file__": str(patch)})
