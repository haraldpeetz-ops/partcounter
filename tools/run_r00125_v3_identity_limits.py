from pathlib import Path

# The current source already contains an additional CompletionHoldArmed validation.
# Temporarily remove that one validation so the deterministic patch anchor matches,
# then restore it after the patch has added JobId validation.
fleet = Path("src/Partcounter.App/Services/MachineFleetService.cs")
fleet_text = fleet.read_text(encoding="utf-8")
armed = '''        if (expectedHoldAfterVeNumber is > 0 && (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) == 0)\n            throw new InvalidOperationException($"{operation}: LOGO! hat HoldAfterVE {expectedHoldAfterVeNumber.Value} bestätigt, aber CompletionHoldArmed ist nicht aktiv.");\n'''
if armed not in fleet_text:
    raise SystemExit("Current CompletionHoldArmed validation not found")
fleet.write_text(fleet_text.replace(armed, "", 1), encoding="utf-8", newline="\n")

patch = Path("tools/apply_r00125_v3_identity_limits.py")
text = patch.read_text(encoding="utf-8")
bad = 'text = replace_once(text, "public void ProtocolV3_BoundaryContractIsAdditive()", "public void ProtocolV3_BoundaryContractIsAdditive()", "protocol test marker")\n'
text = text.replace(bad, "")
code = compile(text, str(patch), "exec")
exec(code, {"__name__": "__main__", "__file__": str(patch)})

fleet_text = fleet.read_text(encoding="utf-8")
job_id_guard = '''        if (expectedJobId.HasValue && snapshot.JobIdEcho != expectedJobId.Value)\n            throw new InvalidOperationException($"{operation}: JobId-Echo {snapshot.JobIdEcho} entspricht nicht Soll {expectedJobId.Value}.");\n'''
if job_id_guard not in fleet_text:
    raise SystemExit("JobId validation was not generated")
fleet.write_text(fleet_text.replace(job_id_guard, job_id_guard + armed, 1), encoding="utf-8", newline="\n")
