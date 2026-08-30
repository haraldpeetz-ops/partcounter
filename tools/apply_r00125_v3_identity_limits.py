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


# 1) Protocol V3: additive JobId echo + explicit LOGO total-cycle limit.
path = "src/Partcounter.App/Services/ModbusRegisterMap.cs"
text = read(path)
text = replace_once(text, "public const ushort StatusLength = 19;", "public const ushort StatusLength = 21;", "StatusLength 21")
text = replace_once(
    text,
    "    public const int StatusHoldAfterVeNumberEcho = 18;\n",
    "    public const int StatusHoldAfterVeNumberEcho = 18;\n    public const int StatusJobIdHiEcho = 19;\n    public const int StatusJobIdLoEcho = 20;\n",
    "JobId status offsets",
)
text = replace_once(
    text,
    "    public const ushort MaxVeNumber = 32_767;\n",
    "    public const ushort MaxVeNumber = 32_767;\n    public const uint MaxTotalCyclesPerJob = 999_999;\n",
    "MaxTotalCyclesPerJob",
)
write(path, text)

# 2) Snapshot carries latched JobId echo.
path = "src/Partcounter.App/Models/PartcounterModels.cs"
text = read(path)
text = replace_once(
    text,
    "    DateTime ReadAtUtc,\n    ushort HoldAfterVeNumberEcho = 0);",
    "    DateTime ReadAtUtc,\n    ushort HoldAfterVeNumberEcho = 0,\n    uint JobIdEcho = 0);",
    "LogoSnapshot JobIdEcho",
)
write(path, text)

# 3) Modbus client validates total-cycle range and reads JobId echo.
path = "src/Partcounter.App/Services/LogoModbusClient.cs"
text = read(path)
text = replace_once(
    text,
    "        var currentParts = checked(currentVeCycles * (uint)activeCavities);\n        var lastCompletedVeQuantity = checked(lastCompletedVeCycles * (uint)lastCompletedCavities);\n\n        return new LogoSnapshot(\n            currentParts,\n            ModbusRegisterMap.ToUInt32(registers[ModbusRegisterMap.StatusTotalCyclesHi], registers[ModbusRegisterMap.StatusTotalCyclesLo]),",
    "        var currentParts = checked(currentVeCycles * (uint)activeCavities);\n        var lastCompletedVeQuantity = checked(lastCompletedVeCycles * (uint)lastCompletedCavities);\n        var totalCycles = ModbusRegisterMap.ToUInt32(\n            registers[ModbusRegisterMap.StatusTotalCyclesHi],\n            registers[ModbusRegisterMap.StatusTotalCyclesLo]);\n        if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)\n            throw new InvalidOperationException(\"LOGO! reported a total-cycle counter outside the approved Partcounter range.\");\n\n        return new LogoSnapshot(\n            currentParts,\n            totalCycles,",
    "total cycle validation",
)
text = replace_once(
    text,
    "            DateTime.UtcNow,\n            registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho]);",
    "            DateTime.UtcNow,\n            registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho],\n            ModbusRegisterMap.ToUInt32(\n                registers[ModbusRegisterMap.StatusJobIdHiEcho],\n                registers[ModbusRegisterMap.StatusJobIdLoEcho]));",
    "JobId echo read",
)
write(path, text)

# 4) Confirmed command handshake also confirms the latched JobId.
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
text = replace_once(
    text,
    "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber);",
    "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber,\n                job.JobId);",
    "SendJob expected JobId",
)
text = replace_once(
    text,
    "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber);",
    "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber,\n                job.JobId);",
    "UpdateVeTarget expected JobId",
)
text = replace_once(
    text,
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null)\n",
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null,\n        uint? expectedJobId = null)\n",
    "ExecuteConfirmedCommand signature",
)
text = replace_once(
    text,
    "                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);",
    "                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber, expectedJobId);",
    "pre-send acknowledgement validation",
)
text = replace_once(
    text,
    "                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber);",
    "                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber, expectedJobId);",
    "wait acknowledgement call",
)
text = replace_once(
    text,
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null)\n    {\n        var stopwatch",
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null,\n        uint? expectedJobId = null)\n    {\n        var stopwatch",
    "WaitForCommandAcknowledgement signature",
)
text = replace_once(
    text,
    "                var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);",
    "                var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber, expectedJobId);",
    "wait validation call",
)
text = replace_once(
    text,
    "        ushort? expectedCavities,\n        ushort? expectedHoldAfterVeNumber = null)\n",
    "        ushort? expectedCavities,\n        ushort? expectedHoldAfterVeNumber = null,\n        uint? expectedJobId = null)\n",
    "ValidateAcknowledgement signature",
)
text = replace_once(
    text,
    "        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)\n            throw new InvalidOperationException($\"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.\");\n        session.LastSnapshot = snapshot;",
    "        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)\n            throw new InvalidOperationException($\"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.\");\n        if (expectedJobId.HasValue && snapshot.JobIdEcho != expectedJobId.Value)\n            throw new InvalidOperationException($\"{operation}: JobId-Echo {snapshot.JobIdEcho} entspricht nicht Soll {expectedJobId.Value}.\");\n        session.LastSnapshot = snapshot;",
    "JobId acknowledgement validation",
)
write(path, text)

# 5) Planning rejects jobs that exceed the native LOGO total-cycle counter.
path = "src/Partcounter.App/Services/VeBoundaryPolicy.cs"
text = read(path)
insert = '''\n    public static uint EstimateTotalCycles(\n        uint orderTargetQuantity,\n        uint standardVeTarget,\n        ushort activeCavities)\n    {\n        if (orderTargetQuantity == 0)\n            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));\n        if (standardVeTarget == 0)\n            throw new ArgumentOutOfRangeException(nameof(standardVeTarget));\n        if (activeCavities is < 1 or > 64)\n            throw new ArgumentOutOfRangeException(nameof(activeCavities));\n\n        ulong remaining = orderTargetQuantity;\n        ulong totalCycles = 0;\n        uint veCount = 0;\n        while (remaining > 0)\n        {\n            veCount++;\n            if (veCount > ModbusRegisterMap.MaxVeNumber)\n                throw new InvalidOperationException($\"Der Auftrag benötigt mehr als {ModbusRegisterMap.MaxVeNumber:N0} VE. Auftrag aufteilen.\");\n\n            var target = (uint)Math.Min((ulong)standardVeTarget, remaining);\n            var cycles = CeilingDivide(target, activeCavities);\n            if (cycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)\n                throw new InvalidOperationException($\"Eine VE benötigt {cycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.\");\n\n            totalCycles += cycles;\n            if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)\n                throw new InvalidOperationException($\"Der Auftrag benötigt {totalCycles:N0} LOGO!-Gesamtzyklen; freigegeben sind maximal {ModbusRegisterMap.MaxTotalCyclesPerJob:N0}. Auftrag aufteilen.\");\n\n            var effectiveQuantity = cycles * activeCavities;\n            remaining = remaining > effectiveQuantity ? remaining - effectiveQuantity : 0;\n        }\n\n        return (uint)totalCycles;\n    }\n'''
text = replace_once(
    text,
    "    private static ulong CeilingDivide(uint numerator, ushort denominator) =>\n",
    insert + "\n    private static ulong CeilingDivide(uint numerator, ushort denominator) =>\n",
    "EstimateTotalCycles method",
)
write(path, text)

# 6) MainViewModel checks total cycles before starting and verifies the boundary hold before DB/label work.
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
text = replace_once(
    text,
    "        VeBoundaryPlan firstPlan;\n        try\n        {\n            firstPlan = VeBoundaryPolicy.Plan(1, 0, OrderTargetQuantity, article.PackagingQuantity, article.ActiveCavities);",
    "        VeBoundaryPlan firstPlan;\n        try\n        {\n            _ = VeBoundaryPolicy.EstimateTotalCycles(OrderTargetQuantity, article.PackagingQuantity, article.ActiveCavities);\n            firstPlan = VeBoundaryPolicy.Plan(1, 0, OrderTargetQuantity, article.PackagingQuantity, article.ActiveCavities);",
    "pre-start total cycle validation",
)
text = replace_once(
    text,
    "        try\n        {\n            await _database.SavePackagingUnitAsync(record);",
    "        try\n        {\n            ushort verifiedScheduledHold = 0;\n            if (!IsSimulationMode)\n                verifiedScheduledHold = await VerifyRealVeBoundaryStateAsync(machine, e);\n\n            await _database.SavePackagingUnitAsync(record);",
    "early boundary verification",
)
text = replace_once(
    text,
    "            if (!IsSimulationMode)\n                await HandleRealVeBoundaryAsync(machine, e);",
    "            if (!IsSimulationMode)\n                await HandleRealVeBoundaryAsync(machine, e, verifiedScheduledHold);",
    "HandleRealVeBoundary call",
)
old_method_start = '''    private async Task HandleRealVeBoundaryAsync(MachineState machine, VeCompletedEventArgs e)\n    {\n        var machineNumber = machine.Configuration.MachineNumber;\n        if (!_scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduledHold) || scheduledHold == 0)\n        {\n            await EnterBoundaryFailSafeAsync(machine, "Für den aktiven Auftrag fehlt ein geplanter LOGO!-Grenzhalt.");\n            throw new InvalidOperationException("Fehlende HoldAfterVE-Planung; Zählung wurde sicherheitshalber pausiert.");\n        }\n\n        if (e.VeNumber < scheduledHold)\n            return;\n\n        if (e.VeNumber > scheduledHold)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"VE {e.VeNumber} wurde abgeschlossen, obwohl der Grenzhalt nach VE {scheduledHold} geplant war.");\n            throw new InvalidOperationException("Geplanter VE-Grenzhalt wurde überschritten.");\n        }\n\n        var diagnostics = _fleet.GetCommunicationDiagnostics(machineNumber);\n        var holdActive = diagnostics is not null &&\n                         (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0;\n        if (!holdActive)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"LOGO! meldet an der geplanten Grenze VE {scheduledHold} keinen aktiven Completion-Hold.");\n            throw new InvalidOperationException("LOGO!-Completion-Hold fehlt an der Sicherheitsgrenze.");\n        }\n'''
new_method_start = '''    private async Task<ushort> VerifyRealVeBoundaryStateAsync(MachineState machine, VeCompletedEventArgs e)\n    {\n        var machineNumber = machine.Configuration.MachineNumber;\n        if (!_scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduledHold) || scheduledHold == 0)\n        {\n            await EnterBoundaryFailSafeAsync(machine, "Für den aktiven Auftrag fehlt ein geplanter LOGO!-Grenzhalt.");\n            throw new InvalidOperationException("Fehlende HoldAfterVE-Planung; Zählung wurde sicherheitshalber pausiert.");\n        }\n\n        if (e.VeNumber < scheduledHold)\n            return scheduledHold;\n\n        if (e.VeNumber > scheduledHold)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"VE {e.VeNumber} wurde abgeschlossen, obwohl der Grenzhalt nach VE {scheduledHold} geplant war.");\n            throw new InvalidOperationException("Geplanter VE-Grenzhalt wurde überschritten.");\n        }\n\n        var diagnostics = _fleet.GetCommunicationDiagnostics(machineNumber);\n        var holdActive = diagnostics is not null &&\n                         (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0;\n        if (!holdActive)\n        {\n            await EnterBoundaryFailSafeAsync(machine, $"LOGO! meldet an der geplanten Grenze VE {scheduledHold} keinen aktiven Completion-Hold.");\n            throw new InvalidOperationException("LOGO!-Completion-Hold fehlt an der Sicherheitsgrenze.");\n        }\n\n        return scheduledHold;\n    }\n\n    private async Task HandleRealVeBoundaryAsync(MachineState machine, VeCompletedEventArgs e, ushort scheduledHold)\n    {\n        var machineNumber = machine.Configuration.MachineNumber;\n        if (e.VeNumber < scheduledHold)\n            return;\n'''
text = replace_once(text, old_method_start, new_method_start, "boundary verification refactor")
write(path, text)

# 7) Regression tests track the V3 contract and LOGO counter ceiling.
path = "tests/Partcounter.Tests/CoreRegressionTests.cs"
text = read(path)
text = replace_once(text, "public void ProtocolV3_BoundaryContractIsAdditive()", "public void ProtocolV3_BoundaryContractIsAdditive()", "protocol test marker")
text = replace_once(text, "Assert.Equal((ushort)19, ModbusRegisterMap.StatusLength);", "Assert.Equal((ushort)21, ModbusRegisterMap.StatusLength);", "StatusLength test")
text = replace_once(
    text,
    "        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);\n",
    "        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);\n        Assert.Equal((uint)999999, ModbusRegisterMap.MaxTotalCyclesPerJob);\n",
    "max total cycles test",
)
write(path, text)

path = "tests/Partcounter.Tests/VeBoundaryPolicyTests.cs"
text = read(path)
insert_test = '''\n    [Fact]\n    public void TotalCycleLimit_IsValidatedBeforeProduction()\n    {\n        Assert.Equal((uint)999999, VeBoundaryPolicy.EstimateTotalCycles(999999, 1000, 1));\n        var ex = Assert.Throws<InvalidOperationException>(() =>\n            VeBoundaryPolicy.EstimateTotalCycles(1_000_000, 1000, 1));\n        Assert.Contains("Gesamtzyklen", ex.Message);\n    }\n'''
text = replace_once(text, "\n    [Fact]\n    public void OrdersBeyondLogoVeRange_AreRejectedBeforeStart()", insert_test + "\n    [Fact]\n    public void OrdersBeyondLogoVeRange_AreRejectedBeforeStart()", "total cycle policy test")
write(path, text)

# 8) Engineering delta; old V2 documents remain historical and unchanged.
write(
    "docs/PROTOCOL_V3_IDENTITY_AND_LIMITS_R001_25.md",
    """# Partcounter R001.25 – Protocol V3 Identität und Zählergrenzen\n\n## Additive Statusregister\nBestehende V3-Adressen bleiben unverändert. Ergänzt werden nur:\n\n- HR39 / VW76: `JobIdEcho_HI`\n- HR40 / VW78: `JobIdEcho_LO`\n\nDie LOGO! muss die beim gültigen Reset-/Auftragskommando über HR8/HR9 empfangene JobId lokal speichern und unverändert auf HR39/HR40 zurückmelden. Partcounter akzeptiert einen bestätigten Auftrag bzw. eine Zielaktualisierung nur, wenn CommandSequence, ErrorCode, ActiveCavitiesEcho, HoldAfterVeNumberEcho **und JobIdEcho** zum Soll passen.\n\n## Freigegebene Gesamtzyklusgrenze\nDer native LOGO!-Gesamtzähler bleibt auf 999.999 Zyklen je Auftrag begrenzt. Partcounter berechnet deshalb vor Produktionsstart die effektive Zykluszahl inklusive kavitätsbedingter VE-Aufrundung. Aufträge oberhalb 999.999 Zyklen oder 32.767 VE werden vor dem Modbus-Start abgewiesen und müssen segmentiert werden.\n\n## Reihenfolge am kritischen VE-Grenzpunkt\nBei einem gemeldeten Abschluss der geplanten `HoldAfterVeNumber` wird `StatusCompletionHoldActive` jetzt **vor** Datenbankpersistenz und Etikettendruck geprüft. Fehlt der lokale Hold, sendet Partcounter sofort den bestätigungspflichtigen Pause-Befehl und protokolliert einen `SAFETY_VE_BOUNDARY_STOP`.\n\n## Reale LOGO!-Freigabe\nDie PC-Seite ist erst dann für M01-Echtbetrieb freigegeben, wenn die LOGO!-V3-Implementierung folgende Signale real liefert:\n\n1. ProtocolVersion = 3\n2. HoldAfterVeNumberEcho auf HR38/VW74\n3. JobIdEcho auf HR39/HR40 bzw. VW76/VW78\n4. Statusbit 6 = CompletionHoldArmed\n5. Statusbit 7 = CompletionHoldActive\n\nVorher bleibt Echtbetrieb absichtlich inkompatibel.\n""",
)

print("R001.25 V3 identity/limit hardening patch applied")
