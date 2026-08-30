from pathlib import Path

patch = Path("tools/apply_r00125_recovery_hold_isolation.py")
text = patch.read_text(encoding="utf-8")

old = '''text = replace_once(\n    text,\n    "            if (wasTemporarilyDisabled)\\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\\n\\n            await PersistPendingActivationAsync",\n    "            if (wasTemporarilyDisabled)\\n            {\\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\\n                await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\\n            }\\n\\n            await PersistPendingActivationAsync",\n    "disabled machine job start suppression",\n)'''

new = '''text = replace_once(\n    text,\n    "            if (wasTemporarilyDisabled)\\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\\n\\n            var liveJobId = JobInstanceIdFactory.Create();",\n    "            if (wasTemporarilyDisabled)\\n            {\\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\\n                await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);\\n            }\\n\\n            var liveJobId = JobInstanceIdFactory.Create();",\n    "disabled machine job start suppression",\n)'''

if old not in text:
    raise SystemExit("Obsolete disabled-machine job-start patch block not found")
text = text.replace(old, new, 1)

code = compile(text, str(patch), "exec")
exec(code, {"__name__": "__main__", "__file__": str(patch)})
