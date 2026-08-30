from pathlib import Path

path = Path("src/Partcounter.App/ViewModels/MainViewModel.cs")
text = path.read_text(encoding="utf-8")

wrong = '''        var now = DateTime.UtcNow;\n        if (!IsSimulationMode)\n            await PersistLiveOrderCheckpointAsync(machine);\n\n        var record = new PackagingUnitRecord(\n            $"TEST-{now:yyyyMMddHHmmssfff}",'''
right = '''        var now = DateTime.UtcNow;\n        var record = new PackagingUnitRecord(\n            $"TEST-{now:yyyyMMddHHmmssfff}",'''
if wrong not in text:
    raise SystemExit("Misplaced TestLabel recovery checkpoint not found")
text = text.replace(wrong, right, 1)

anchor = '''        }\n\n        var record = new PackagingUnitRecord(\n            $"PC-{completedUtc:yyyyMMddHHmmssfff}-M{machine.Configuration.MachineNumber:00}-VE{e.VeNumber:0000}",'''
replacement = '''        }\n\n        if (!IsSimulationMode)\n            await PersistLiveOrderCheckpointAsync(machine);\n\n        var record = new PackagingUnitRecord(\n            $"PC-{completedUtc:yyyyMMddHHmmssfff}-M{machine.Configuration.MachineNumber:00}-VE{e.VeNumber:0000}",'''
if anchor not in text:
    raise SystemExit("MachineOnVeCompleted checkpoint anchor not found")
text = text.replace(anchor, replacement, 1)

path.write_text(text, encoding="utf-8", newline="\n")
print("R001.25 recovery compile fix applied")
