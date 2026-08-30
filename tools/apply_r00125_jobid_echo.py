from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    p = Path(path)
    p.write_text(text, encoding="utf-8", newline="\n")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"Pattern not found for {label}")
    return text.replace(old, new, 1)


# Modbus V3 status extension: existing addresses remain untouched.
path = "src/Partcounter.App/Services/ModbusRegisterMap.cs"
text = read(path)
text = replace_once(text, "public const ushort StatusLength = 19;", "public const ushort StatusLength = 21;", "StatusLength")
text = replace_once(
    text,
    "    public const int StatusHoldAfterVeNumberEcho = 18;\n",
    "    public const int StatusHoldAfterVeNumberEcho = 18;\n    public const int StatusJobIdHiEcho = 19;\n    public const int StatusJobIdLoEcho = 20;\n",
    "JobId status offsets",
)
write(path, text)

# Snapshot exposes the LOGO-side latched job identity.
path = "src/Partcounter.App/Models/PartcounterModels.cs"
text = read(path)
text = replace_once(
    text,
    "    DateTime ReadAtUtc,\n    ushort HoldAfterVeNumberEcho = 0);",
    "    DateTime ReadAtUtc,\n    ushort HoldAfterVeNumberEcho = 0,\n    uint JobIdEcho = 0);",
    "LogoSnapshot JobIdEcho",
)
write(path, text)

# Read the two additional registers and reject impossible total counters.
path = "src/Partcounter.App/Services/LogoModbusClient.cs"
text = read(path)
text = replace_once(
    text,
    "        var currentParts = checked(currentVeCycles * (uint)activeCavities);\n        var lastCompletedVeQuantity = checked(lastCompletedVeCycles * (uint)lastCompletedCavities);\n\n        return new LogoSnapshot(\n            currentParts,\n            ModbusRegisterMap.ToUInt32(registers[ModbusRegisterMap.StatusTotalCyclesHi], registers[ModbusRegisterMap.StatusTotalCyclesLo]),",
    "        var currentParts = checked(currentVeCycles * (uint)activeCavities);\n        var lastCompletedVeQuantity = checked(lastCompletedVeCycles * (uint)lastCompletedCavities);\n        var totalCycles = ModbusRegisterMap.ToUInt32(\n            registers[ModbusRegisterMap.StatusTotalCyclesHi],\n            registers[ModbusRegisterMap.StatusTotalCyclesLo]);\n        if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)\n            throw new InvalidOperationException(\"LOGO! reported a total-cycle counter outside the approved Partcounter V3 range.\");\n\n        return new LogoSnapshot(\n            currentParts,\n            totalCycles,",
    "total-cycle read validation",
)
text = replace_once(
    text,
    "            DateTime.UtcNow,\n            registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho]);",
    "            DateTime.UtcNow,\n            registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho],\n            ModbusRegisterMap.ToUInt32(\n                registers[ModbusRegisterMap.StatusJobIdHiEcho],\n                registers[ModbusRegisterMap.StatusJobIdLoEcho]));",
    "JobId echo read",
)
write(path, text)

# Every job/config acknowledgement now proves that the LOGO holds the intended job ID.
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
call_old = "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber);"
call_new = "                job.ActiveCavities,\n                cancellationToken,\n                job.HoldAfterVeNumber,\n                job.JobId);"
text = replace_once(text, call_old, call_new, "SendJob JobId")
text = replace_once(text, call_old, call_new, "UpdateVeTarget JobId")
text = replace_once(
    text,
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null)\n    {\n        Exception? lastError",
    "        CancellationToken cancellationToken,\n        ushort? expectedHoldAfterVeNumber = null,\n        uint? expectedJobId = null)\n    {\n        Exception? lastError",
    "ExecuteConfirmedCommand signature",
)
text = replace_once(
    text,
    "                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);",
    "                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber, expectedJobId);",
    "pre-send validation call",
)
text = replace_once(
    text,
    "                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber);",
    "                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber, expectedJobId);",
    "wait call",
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
    "        ushort? expectedCavities,\n        ushort? expectedHoldAfterVeNumber = null)\n    {",
    "        ushort? expectedCavities,\n        ushort? expectedHoldAfterVeNumber = null,\n        uint? expectedJobId = null)\n    {",
    "ValidateAcknowledgement signature",
)
text = replace_once(
    text,
    "        if (expectedHoldAfterVeNumber is > 0 && (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) == 0)\n            throw new InvalidOperationException($\"{operation}: LOGO! hat HoldAfterVE {expectedHoldAfterVeNumber.Value} bestätigt, aber CompletionHoldArmed ist nicht aktiv.\");\n        session.LastSnapshot = snapshot;",
    "        if (expectedJobId.HasValue && snapshot.JobIdEcho != expectedJobId.Value)\n            throw new InvalidOperationException($\"{operation}: JobId-Echo {snapshot.JobIdEcho} entspricht nicht Soll {expectedJobId.Value}.\");\n        if (expectedHoldAfterVeNumber is > 0 && (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) == 0)\n            throw new InvalidOperationException($\"{operation}: LOGO! hat HoldAfterVE {expectedHoldAfterVeNumber.Value} bestätigt, aber CompletionHoldArmed ist nicht aktiv.\");\n        session.LastSnapshot = snapshot;",
    "JobId acknowledgement guard",
)
write(path, text)

# Regression contract for additive status registers and existing capacity ceiling.
path = "tests/Partcounter.Tests/CoreRegressionTests.cs"
text = read(path)
text = replace_once(text, "Assert.Equal((ushort)19, ModbusRegisterMap.StatusLength);", "Assert.Equal((ushort)21, ModbusRegisterMap.StatusLength);", "status length test")
text = replace_once(
    text,
    "        Assert.Equal(18, ModbusRegisterMap.StatusHoldAfterVeNumberEcho);\n        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);",
    "        Assert.Equal(18, ModbusRegisterMap.StatusHoldAfterVeNumberEcho);\n        Assert.Equal(19, ModbusRegisterMap.StatusJobIdHiEcho);\n        Assert.Equal(20, ModbusRegisterMap.StatusJobIdLoEcho);\n        Assert.Equal((uint)999999, ModbusRegisterMap.MaxTotalCyclesPerJob);\n        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);",
    "JobId contract tests",
)
write(path, text)

# Engineering delta for the real LOGO implementation.
doc = Path("docs/PROTOCOL_V3_JOB_ID_ECHO_R001_25.md")
doc.write_text(
    """# Partcounter R001.25 – Protocol V3 JobId-Echo\n\n## Zweck\nFür einen sicheren Wiederanlauf nach PC-/Partcounter-Neustart muss der PC eindeutig erkennen können, welcher Produktionsauftrag tatsächlich in der Siemens LOGO! aktiv ist. Kavitäten- und Hold-Echo allein reichen dafür nicht.\n\n## Additive Register\nAlle bestehenden V3-Adressen bleiben unverändert. Ergänzt werden am Ende des Statusbereichs:\n\n| HR | PC-Offset | LOGO VM | Bedeutung |\n|---:|---:|---|---|\n| HR39 | 38 | VW76 | JobIdEcho High Word |\n| HR40 | 39 | VW78 | JobIdEcho Low Word |\n\n`StatusLength` steigt damit von 19 auf 21 Register.\n\nDie LOGO! übernimmt die bei einem gültigen neuen Auftrag empfangene `JobId` aus HR8/HR9 in einen lokalen, stabilen Auftragsidentitätswert und gibt diesen auf HR39/HR40 zurück. Zielupdates innerhalb desselben Auftrags dürfen die JobId nicht verändern.\n\n## PC-Prüfung\nEin Auftrag oder VE-Zielupdate gilt nur dann als bestätigt, wenn gleichzeitig gelten:\n\n- AckSequence = gesendete CommandSequence\n- ErrorCode = 0\n- ActiveCavitiesEcho = Soll\n- HoldAfterVeNumberEcho = Soll\n- JobIdEcho = Soll\n- bei Hold > 0: CompletionHoldArmed ist aktiv\n\nEine Abweichung wird als Kommunikations-/Protokollfehler behandelt; Partcounter gibt die Zählung nicht frei.\n\n## Gesamtzyklusgrenze\nDie bereits implementierte und getestete Grenze von 999.999 LOGO!-Gesamtzyklen je Auftrag bleibt unverändert. Werte oberhalb der freigegebenen Grenze werden vor Auftragsstart abgewiesen/segmentiert.\n\n## Reale Abnahme M01\nVor Echtfreigabe muss an der realen LOGO! geprüft werden, dass JobIdEcho nach Reset/Auftragsstart korrekt gesetzt wird, über normale VE-Wechsel und Zielupdates stabil bleibt und nach LOGO-Neustart dem freigegebenen Retentivitätskonzept entspricht.\n""",
    encoding="utf-8",
    newline="\n",
)

print("Focused R001.25 Protocol V3 JobId echo patch applied")
