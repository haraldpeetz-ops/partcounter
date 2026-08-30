from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8", newline="\n")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"Pattern not found for {label}")
    return text.replace(old, new, 1)


# LOGO command: a manual VE change must never release counting implicitly.
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
text = replace_once(
    text,
    "token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandManualVeChange), token),",
    "token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandPauseCounting | ModbusRegisterMap.CommandManualVeChange), token),",
    "manual VE command keeps pause bit",
)
write(path, text)

# Main UI/control logic: confirmed pause -> manual completion -> deterministic replanning -> confirmed resume.
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
text = replace_once(
    text,
    "    private readonly Dictionary<int, ushort> _scheduledCompletionHolds = new();\n",
    "    private readonly Dictionary<int, ushort> _scheduledCompletionHolds = new();\n    private readonly HashSet<int> _manualVeReconfigurationPending = new();\n",
    "manual pending field",
)

old_manual = '''    private async Task ManualVeChangeAsync(object? parameter)\n    {\n        if (parameter is not MachineState machine) return;\n\n        if (!machine.IsActiveOrder || machine.IsTemporarilyDisabled)\n        {\n            StatusMessage = $"{machine.DisplayName}: Kein aktiver Auftrag.";\n            return;\n        }\n\n        if (IsSimulationMode)\n        {\n            machine.CompleteCurrentVe(VeCompletionReason.Manual);\n            return;\n        }\n\n        try\n        {\n            await _fleet.SendManualVeChangeAsync(machine.Configuration.MachineNumber);\n            StatusMessage = $"Manueller VE-Wechsel an {machine.DisplayName} angefordert.";\n        }\n        catch (Exception ex)\n        {\n            StatusMessage = $"VE-Wechsel fehlgeschlagen: {ex.Message}";\n        }\n    }\n'''
new_manual = '''    private async Task ManualVeChangeAsync(object? parameter)\n    {\n        if (parameter is not MachineState machine) return;\n\n        if (!machine.IsActiveOrder || machine.IsTemporarilyDisabled)\n        {\n            StatusMessage = $"{machine.DisplayName}: Kein aktiver Auftrag.";\n            return;\n        }\n\n        if (IsSimulationMode)\n        {\n            machine.CompleteCurrentVe(VeCompletionReason.Manual);\n            return;\n        }\n\n        var machineNumber = machine.Configuration.MachineNumber;\n        if (_manualVeReconfigurationPending.Contains(machineNumber))\n        {\n            StatusMessage = $"{machine.DisplayName}: Ein manueller VE-Wechsel wartet bereits auf eindeutige Abschluss-/Neuplanungsbestätigung.";\n            return;\n        }\n\n        var pauseConfirmed = false;\n        try\n        {\n            await _fleet.PauseCountingAsync(machineNumber);\n            pauseConfirmed = true;\n            _manualVeReconfigurationPending.Add(machineNumber);\n\n            await _fleet.SendManualVeChangeAsync(machineNumber);\n            StatusMessage = $"{machine.DisplayName}: Manueller VE-Wechsel bestätigt angefordert. Zählung bleibt bis zur Neuplanung gesperrt.";\n            await _database.AddEventAsync(machineNumber, "MANUAL_VE_CHANGE_ARMED", StatusMessage);\n        }\n        catch (Exception ex)\n        {\n            if (pauseConfirmed)\n            {\n                if (machine.OrderState == ProductionOrderState.Running)\n                    machine.PauseOrder();\n\n                StatusMessage = $"{machine.DisplayName}: Manueller VE-Wechsel nicht eindeutig bestätigt. Zählung bleibt gesperrt; Abschluss abwarten oder kontrolliert zurücksetzen. {ex.Message}";\n                await _database.AddEventAsync(machineNumber, "MANUAL_VE_CHANGE_UNCERTAIN", StatusMessage);\n            }\n            else\n            {\n                StatusMessage = $"{machine.DisplayName}: Manueller VE-Wechsel nicht gestartet, weil die Zählpause nicht bestätigt wurde. {ex.Message}";\n                await _database.AddEventAsync(machineNumber, "MANUAL_VE_CHANGE_REJECTED", StatusMessage);\n            }\n        }\n    }\n'''
text = replace_once(text, old_manual, new_manual, "manual VE method")

text = replace_once(
    text,
    "        if (machine.IsTemporarilyDisabled)\n        {\n            StatusMessage = \"Maschine zuerst wieder aktivieren.\";\n            return;\n        }\n\n        try\n",
    "        if (machine.IsTemporarilyDisabled)\n        {\n            StatusMessage = \"Maschine zuerst wieder aktivieren.\";\n            return;\n        }\n\n        if (_manualVeReconfigurationPending.Contains(machine.Configuration.MachineNumber))\n        {\n            StatusMessage = \"Fortsetzen gesperrt: Ein manueller VE-Wechsel ist noch nicht eindeutig abgeschlossen und neu geplant.\";\n            return;\n        }\n\n        try\n",
    "resume guard for uncertain manual VE",
)

text = replace_once(
    text,
    "            _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);\n            StatusMessage =\n                $\"{machine.DisplayName}: Auftrag {machine.OrderNumber} beendet",
    "            _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);\n            _manualVeReconfigurationPending.Remove(machine.Configuration.MachineNumber);\n            StatusMessage =\n                $\"{machine.DisplayName}: Auftrag {machine.OrderNumber} beendet",
    "end order clears manual pending",
)

text = replace_once(
    text,
    "            machine.ResetCounters();\n            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;\n            StatusMessage = $\"{machine.DisplayName}: Auftragszähler zurückgesetzt.\";",
    "            machine.ResetCounters();\n            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;\n            _manualVeReconfigurationPending.Remove(machine.Configuration.MachineNumber);\n            StatusMessage = $\"{machine.DisplayName}: Auftragszähler zurückgesetzt.\";",
    "simulation reset clears manual pending",
)
text = replace_once(
    text,
    "            machine.ResetCounters();\n            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;\n            StatusMessage = $\"Reset an {machine.DisplayName} gesendet; Grenzhalt",
    "            machine.ResetCounters();\n            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;\n            _manualVeReconfigurationPending.Remove(machine.Configuration.MachineNumber);\n            StatusMessage = $\"Reset an {machine.DisplayName} gesendet; Grenzhalt",
    "real reset clears manual pending",
)

text = replace_once(
    text,
    "        var atHeldBoundary = false;\n        string? boundaryError = null;\n\n        if (!IsSimulationMode)\n        {\n            (atHeldBoundary, boundaryError) = await PrecheckRealVeBoundaryAsync(machine, e);\n        }",
    "        var atHeldBoundary = false;\n        string? boundaryError = null;\n        var expectedManualReconfiguration = !IsSimulationMode &&\n                                            e.Reason == VeCompletionReason.Manual &&\n                                            _manualVeReconfigurationPending.Contains(machine.Configuration.MachineNumber);\n\n        if (!IsSimulationMode)\n        {\n            if (e.Reason == VeCompletionReason.Manual && !expectedManualReconfiguration)\n            {\n                boundaryError = \"Unerwarteter manueller VE-Abschluss ohne zuvor bestätigte Zählpause.\";\n                await EnterBoundaryFailSafeAsync(machine, boundaryError);\n            }\n            else if (!expectedManualReconfiguration)\n            {\n                (atHeldBoundary, boundaryError) = await PrecheckRealVeBoundaryAsync(machine, e);\n            }\n        }",
    "manual completion precheck",
)

text = replace_once(
    text,
    "            if (!IsSimulationMode && atHeldBoundary)\n                await ContinueAfterHeldBoundaryAsync(machine, e);\n\n            StatusMessage = machine.OrderState == ProductionOrderState.Completed",
    "            if (!IsSimulationMode && expectedManualReconfiguration)\n            {\n                await ContinueAfterManualVeChangeAsync(machine, e);\n                StatusMessage = machine.OrderState == ProductionOrderState.Completed\n                    ? $\"{machine.DisplayName}: Manueller VE-Abschluss {e.VeNumber} protokolliert; Auftrag abgeschlossen und Zählung bleibt gesperrt.\"\n                    : $\"{machine.DisplayName}: Manueller VE-Abschluss {e.VeNumber} protokolliert; nächste VE {machine.CurrentVeTargetParts:N0} Teile sicher neu geplant.\";\n                return;\n            }\n\n            if (!IsSimulationMode && atHeldBoundary)\n                await ContinueAfterHeldBoundaryAsync(machine, e);\n\n            StatusMessage = machine.OrderState == ProductionOrderState.Completed",
    "manual continuation dispatch",
)

insert_method = '''\n    private async Task ContinueAfterManualVeChangeAsync(MachineState machine, VeCompletedEventArgs e)\n    {\n        var machineNumber = machine.Configuration.MachineNumber;\n\n        if (machine.OrderState == ProductionOrderState.Completed)\n        {\n            await _fleet.PauseCountingAsync(machineNumber);\n            _scheduledCompletionHolds.Remove(machineNumber);\n            _manualVeReconfigurationPending.Remove(machineNumber);\n            await _database.AddEventAsync(machineNumber, "MANUAL_VE_FINAL",\n                $"Auftrag {machine.OrderNumber}: manueller Abschluss VE {e.VeNumber}; Auftrag vollständig, Zählpause bestätigt.");\n            return;\n        }\n\n        if (!machine.IsActiveOrder || machine.CurrentVeTargetParts == 0)\n        {\n            const string reason = "Nach manuellem VE-Wechsel ist kein gültiges nächstes VE-Ziel vorhanden.";\n            await EnterBoundaryFailSafeAsync(machine, reason);\n            throw new InvalidOperationException(reason);\n        }\n\n        VeBoundaryPlan nextPlan;\n        try\n        {\n            nextPlan = VeBoundaryPolicy.Plan(\n                machine.CurrentVeNumber,\n                machine.OrderProducedQuantity,\n                machine.OrderTargetQuantity,\n                machine.TargetPartsPerVe,\n                machine.ActiveCavities);\n        }\n        catch (Exception ex)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"Neuplanung nach manuellem VE-Wechsel fehlgeschlagen: {ex.Message}");\n            throw;\n        }\n\n        if (nextPlan.TargetParts != machine.CurrentVeTargetParts)\n        {\n            var reason = $"Planungsabweichung nach manuellem VE-Wechsel: MachineState {machine.CurrentVeTargetParts}, Grenzplan {nextPlan.TargetParts} Teile.";\n            await EnterBoundaryFailSafeAsync(machine, reason);\n            throw new InvalidOperationException(reason);\n        }\n\n        var nextJob = new JobParameters(\n            StableUInt32(machine.OrderNumber),\n            machine.ArticleNumber,\n            machine.ToolNumber,\n            machine.ActiveCavities,\n            nextPlan.TargetParts,\n            nextPlan.TargetCycles,\n            ValvePulseMs,\n            nextPlan.HoldAfterVeNumber);\n\n        try\n        {\n            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);\n            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;\n            _manualVeReconfigurationPending.Remove(machineNumber);\n            if (machine.OrderState == ProductionOrderState.Running)\n                await _fleet.ResumeCountingAsync(machineNumber);\n        }\n        catch (Exception ex)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"Neuplanung/Freigabe nach manuellem VE-Wechsel fehlgeschlagen: {ex.Message}");\n            throw;\n        }\n\n        await _database.AddEventAsync(machineNumber, "MANUAL_VE_RECONFIGURED",\n            $"Nach manuellem Abschluss VE {e.VeNumber}: Ziel {nextPlan.TargetParts} Teile; nächster Hold VE {nextPlan.HoldAfterVeNumber}.");\n    }\n'''
text = replace_once(
    text,
    "\n    private async Task<(bool AtHeldBoundary, string? Error)> PrecheckRealVeBoundaryAsync(",
    insert_method + "\n    private async Task<(bool AtHeldBoundary, string? Error)> PrecheckRealVeBoundaryAsync(",
    "insert manual continuation method",
)
write(path, text)

# Regression tests document deterministic replanning after manual partial completion.
path = "tests/Partcounter.Tests/VeBoundaryPolicyTests.cs"
text = read(path)
manual_tests = '''\n    [Fact]\n    public void ManualPartialVe_ReplansNextSafeBoundary()\n    {\n        var plan = VeBoundaryPolicy.Plan(2, 512, 2500, 1000, 64);\n        Assert.Equal((uint)1000, plan.TargetParts);\n        Assert.Equal((uint)16, plan.TargetCycles);\n        Assert.Equal((ushort)2, plan.HoldAfterVeNumber);\n    }\n\n    [Fact]\n    public void ManualCompletionAtBoundary_ReplansPartialFollowingVe()\n    {\n        var plan = VeBoundaryPolicy.Plan(3, 1536, 2500, 1000, 64);\n        Assert.Equal((uint)964, plan.TargetParts);\n        Assert.Equal((uint)16, plan.TargetCycles);\n        Assert.Equal((ushort)3, plan.HoldAfterVeNumber);\n    }\n'''
text = replace_once(
    text,
    "\n    [Fact]\n    public void LogoTotalCycleLimit_IsRejectedBeforeOrderStart()",
    manual_tests + "\n    [Fact]\n    public void LogoTotalCycleLimit_IsRejectedBeforeOrderStart()",
    "manual VE policy tests",
)
write(path, text)

Path("docs/MANUAL_VE_FAILSAFE_R001_25.md").write_text(
    """# Partcounter R001.25 – manueller VE-Wechsel fail-safe\n\n## Ziel\nEin manueller VE-Wechsel verändert die tatsächlich produzierte Menge einer VE und darf deshalb niemals mit einer veralteten Grenzplanung weiterlaufen.\n\n## Echtbetriebssequenz\n1. Partcounter sendet `PauseCounting` und wartet auf Ack.\n2. Erst nach bestätigter Pause wird `ManualVeChange` gesendet; das Manual-Kommando trägt das Pause-Bit weiterhin.\n3. Bis zum eindeutig erkannten manuellen `CompletionSequence`-Ereignis wird keine Zählfreigabe erteilt.\n4. Nach dem Abschluss berechnet Partcounter aus Auftragsrest, Kavitäten und aktueller VE-Nummer ein neues Ziel und `HoldAfterVeNumber`.\n5. Ziel/Hold werden mit `pauseCounting=true` geschrieben und vollständig bestätigt.\n6. Nur bei zuvor laufendem Auftrag folgt ein bestätigtes Resume.\n\n## Unsichere Kommunikation\nIst die Pause bestätigt, aber die Antwort auf den manuellen Wechsel geht verloren, bleibt der Auftrag gesperrt. Ein später eindeutig erkanntes Manual-Completion-Ereignis kann die Neuplanung abschließen. Ohne eindeutigen Abschluss ist ein normales Resume gesperrt; es ist ein kontrollierter Reset oder Abbruch erforderlich.\n\nEin unerwarteter manueller VE-Abschluss ohne zuvor bestätigte Pause erzeugt `SAFETY_VE_BOUNDARY_STOP`.\n""",
    encoding="utf-8",
    newline="\n",
)

print("R001.25 manual VE fail-safe hardening patch applied")
