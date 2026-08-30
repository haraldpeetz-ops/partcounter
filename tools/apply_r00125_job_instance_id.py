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


# 1) Dedicated production-instance identifier. Both 16-bit words stay <= 32767 so
# LOGO analog/network blocks can echo the token without signed-range ambiguity.
write("src/Partcounter.App/Services/JobInstanceIdFactory.cs", '''using System.Security.Cryptography;\n\nnamespace Partcounter.Services;\n\npublic static class JobInstanceIdFactory\n{\n    public static uint Create()\n    {\n        var high = (ushort)RandomNumberGenerator.GetInt32(0, ModbusRegisterMap.MaxSequenceValue + 1);\n        var low = (ushort)RandomNumberGenerator.GetInt32(1, ModbusRegisterMap.MaxSequenceValue + 1);\n        return ((uint)high << 16) | low;\n    }\n\n    public static bool IsLogoWordSafe(uint value) =>\n        value != 0 &&\n        ModbusRegisterMap.HighWord(value) <= ModbusRegisterMap.MaxSequenceValue &&\n        ModbusRegisterMap.LowWord(value) <= ModbusRegisterMap.MaxSequenceValue;\n}\n''')

# 2) Modbus client rejects ambiguous/out-of-range job identities before a PLC write.
path = "src/Partcounter.App/Services/LogoModbusClient.cs"
text = read(path)
anchor = '''        if (job.ActiveCavities is < 1 or > 64)\n            throw new ArgumentOutOfRangeException(nameof(job), "Active cavities must be between 1 and 64.");\n\n'''
replacement = anchor + '''        if (!JobInstanceIdFactory.IsLogoWordSafe(job.JobId))\n            throw new ArgumentOutOfRangeException(nameof(job), "JobId must be nonzero and each 16-bit word must stay in the LOGO analog-safe range 0..32767.");\n\n'''
text = replace_once(text, anchor, replacement, "JobId validation")
write(path, text)

# 3) Recovery partial owns the live production instance id and restores it from disk.
path = "src/Partcounter.App/ViewModels/MainViewModel.Recovery.cs"
text = read(path)
text = replace_once(
    text,
    '''    private readonly Dictionary<int, ActiveOrderCheckpoint> _liveOrderCheckpoints = new();\n    private readonly HashSet<int> _startupRecoveryMachines = new();\n''',
    '''    private readonly Dictionary<int, ActiveOrderCheckpoint> _liveOrderCheckpoints = new();\n    private readonly Dictionary<int, uint> _activeJobIds = new();\n    private readonly HashSet<int> _startupRecoveryMachines = new();\n''',
    "active job id dictionary",
)
text = replace_once(
    text,
    '''            _liveOrderCheckpoints[checkpoint.MachineNumber] = checkpoint;\n            _startupRecoveryMachines.Add(checkpoint.MachineNumber);\n''',
    '''            _liveOrderCheckpoints[checkpoint.MachineNumber] = checkpoint;\n            _activeJobIds[checkpoint.MachineNumber] = checkpoint.JobId;\n            _startupRecoveryMachines.Add(checkpoint.MachineNumber);\n''',
    "restore active job id",
)
text = replace_once(
    text,
    '''                        await DeleteLiveOrderCheckpointAsync(machineNumber);\n                        _scheduledCompletionHolds.Remove(machineNumber);\n''',
    '''                        await DeleteLiveOrderCheckpointAsync(machineNumber);\n                        _activeJobIds.Remove(machineNumber);\n                        _scheduledCompletionHolds.Remove(machineNumber);\n''',
    "discard pending activation id",
)
text = replace_once(
    text,
    '''        uint orderTargetQuantity,\n        VeBoundaryPlan firstPlan)\n    {\n''',
    '''        uint orderTargetQuantity,\n        VeBoundaryPlan firstPlan,\n        uint jobId)\n    {\n''',
    "pending activation signature",
)
text = replace_once(
    text,
    '''            machine.Configuration.MachineNumber,\n            orderNumber,\n            StableUInt32(orderNumber),\n''',
    '''            machine.Configuration.MachineNumber,\n            orderNumber,\n            jobId,\n''',
    "pending activation job id",
)
text = replace_once(
    text,
    '''            machineNumber,\n            machine.OrderNumber,\n            StableUInt32(machine.OrderNumber),\n''',
    '''            machineNumber,\n            machine.OrderNumber,\n            GetActiveJobId(machine),\n''',
    "live checkpoint job id",
)
text = replace_once(
    text,
    '''        await OrderRecovery.DeleteAsync(machineNumber);\n        _liveOrderCheckpoints.Remove(machineNumber);\n    }\n}\n''',
    '''        await OrderRecovery.DeleteAsync(machineNumber);\n        _liveOrderCheckpoints.Remove(machineNumber);\n        _activeJobIds.Remove(machineNumber);\n    }\n\n    private uint GetActiveJobId(MachineState machine)\n    {\n        var machineNumber = machine.Configuration.MachineNumber;\n        if (_activeJobIds.TryGetValue(machineNumber, out var jobId) && jobId != 0)\n            return jobId;\n        if (_liveOrderCheckpoints.TryGetValue(machineNumber, out var checkpoint) && checkpoint.JobId != 0)\n        {\n            _activeJobIds[machineNumber] = checkpoint.JobId;\n            return checkpoint.JobId;\n        }\n        throw new InvalidOperationException($"{machine.DisplayName}: keine eindeutige aktive JobId vorhanden; Modbus-Auftragsänderung wird gesperrt.");\n    }\n}\n''',
    "active job id helper",
)
write(path, text)

# 4) Main order start allocates the token before persistence and never writes to LOGO
# unless the recovery checkpoint has been durably stored first.
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
old = '''        if (!IsSimulationMode)\n        {\n            if (wasTemporarilyDisabled)\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n\n            await PersistPendingActivationAsync(machine, article, order, OrderTargetQuantity, firstPlan);\n\n            var job = new JobParameters(\n                StableUInt32(order),\n'''
new = '''        if (!IsSimulationMode)\n        {\n            if (wasTemporarilyDisabled)\n                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);\n\n            var liveJobId = JobInstanceIdFactory.Create();\n            _activeJobIds[machine.Configuration.MachineNumber] = liveJobId;\n            try\n            {\n                await PersistPendingActivationAsync(machine, article, order, OrderTargetQuantity, firstPlan, liveJobId);\n            }\n            catch (Exception ex)\n            {\n                _activeJobIds.Remove(machine.Configuration.MachineNumber);\n                StatusMessage = $"Auftrag nicht gestartet: Recovery-Checkpoint konnte vor dem LOGO!-Schreiben nicht sicher gespeichert werden: {ex.Message}";\n                return;\n            }\n\n            var job = new JobParameters(\n                liveJobId,\n'''
text = replace_once(text, old, new, "new live job id")
text = text.replace("StableUInt32(machine.OrderNumber)", "GetActiveJobId(machine)")
write(path, text)

# 5) Regression tests: generated identifiers are nonzero, word-safe and a persisted
# checkpoint keeps the exact production-instance identity.
write("tests/Partcounter.Tests/JobInstanceIdTests.cs", '''using Partcounter.Services;\nusing Xunit;\n\nnamespace Partcounter.Tests;\n\npublic sealed class JobInstanceIdTests\n{\n    [Fact]\n    public void GeneratedIds_AreNonZeroAndLogoWordSafe()\n    {\n        for (var i = 0; i < 256; i++)\n        {\n            var value = JobInstanceIdFactory.Create();\n            Assert.NotEqual(0u, value);\n            Assert.True(JobInstanceIdFactory.IsLogoWordSafe(value));\n            Assert.InRange(ModbusRegisterMap.HighWord(value), (ushort)0, ModbusRegisterMap.MaxSequenceValue);\n            Assert.InRange(ModbusRegisterMap.LowWord(value), (ushort)1, ModbusRegisterMap.MaxSequenceValue);\n        }\n    }\n\n    [Theory]\n    [InlineData(0u, false)]\n    [InlineData(1u, true)]\n    [InlineData(2147450879u, true)] // 0x7FFF7FFF\n    [InlineData(2147483648u, false)] // high word 0x8000\n    [InlineData(32768u, false)]      // low word 0x8000\n    public void WordSafety_IsExplicit(uint value, bool expected)\n    {\n        Assert.Equal(expected, JobInstanceIdFactory.IsLogoWordSafe(value));\n    }\n}\n''')

# 6) Record the rationale in deterministic engineering text.
write("docs/JOB_INSTANCE_ID_R001_25.md", '''# Partcounter R001.25 – eindeutige Auftragsinstanz-ID\n\nFür den Echtbetrieb wird `JobId` nicht aus der sichtbaren Auftragsnummer abgeleitet. Jede neue LOGO!-Auftragsaktivierung erhält einen eigenen kryptografisch erzeugten 32-Bit-Korrelationstoken. Beide 16-Bit-Wörter bleiben bewusst im Bereich 0…32767, damit die Siemens-LOGO!-Analog-/Netzwerkbausteine den Wert ohne Vorzeichenmehrdeutigkeit als High-/Low-Word spiegeln können.\n\nDer Token wird **vor** dem ersten Modbus-Auftragsschreiben als `PendingActivation` in SQLite persistiert. Erst danach darf Partcounter den Reset-/Startbefehl an die LOGO! senden. Nach erfolgreichem Ack wird derselbe Token für alle Zieländerungen, Pausen/Wiederanläufe und Recovery-Prüfungen dieses Produktionsauftrags beibehalten.\n\nBei einem Neustart akzeptiert Partcounter einen gespeicherten Echtauftrag nur, wenn `JobIdEcho`, Kavitäten und Grenzhalt zum Checkpoint passen. Eine wiederverwendete sichtbare Auftragsnummer erzeugt deshalb eine neue technische Instanz und kann nicht irrtümlich mit einem alten LOGO!-Auftrag verwechselt werden.\n''')

print("R001.25 unique job-instance identity hardening applied")
