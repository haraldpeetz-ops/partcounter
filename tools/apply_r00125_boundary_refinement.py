from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8", newline="\n")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"Pattern not found: {label}")
    return text.replace(old, new, 1)


def replace_between(text: str, start: str, end: str, replacement: str, label: str) -> str:
    i = text.find(start)
    if i < 0:
        raise SystemExit(f"Start marker not found: {label}")
    j = text.find(end, i)
    if j < 0:
        raise SystemExit(f"End marker not found: {label}")
    return text[:i] + replacement + text[j:]


# LOGO total-cycle capacity is a hard engineering limit for the current standardized program.
path = "src/Partcounter.App/Services/ModbusRegisterMap.cs"
text = read(path)
text = replace_once(text,
    "    public const uint MaxTargetCyclesPerVe = 32_767;\n",
    "    public const uint MaxTargetCyclesPerVe = 32_767;\n    public const uint MaxTotalCyclesPerJob = 999_999;\n",
    "MaxTotalCyclesPerJob")
write(path, text)


# Rewrite the pure planner with a complete order-capacity preflight.
write("src/Partcounter.App/Services/VeBoundaryPolicy.cs", '''namespace Partcounter.Services;

public sealed record VeBoundaryPlan(
    uint TargetParts,
    uint TargetCycles,
    uint EffectiveQuantity,
    ushort HoldAfterVeNumber);

public sealed record OrderCapacityPlan(
    uint RequiredTotalCycles,
    ushort RequiredPackagingUnits);

public static class VeBoundaryPolicy
{
    public static VeBoundaryPlan Plan(
        ushort currentVeNumber,
        uint producedBeforeCurrentVe,
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        ValidateBasicInputs(currentVeNumber, producedBeforeCurrentVe, orderTargetQuantity, standardVeTarget, activeCavities);

        if (currentVeNumber == 1 && producedBeforeCurrentVe == 0)
            _ = CalculateOrderCapacity(orderTargetQuantity, standardVeTarget, activeCavities);

        var remaining = (ulong)orderTargetQuantity - producedBeforeCurrentVe;
        var currentTarget = (uint)Math.Min((ulong)standardVeTarget, remaining);
        var currentCycles = CeilingDivide(currentTarget, activeCavities);
        if (currentCycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
            throw new InvalidOperationException($"Das VE-Ziel benötigt {currentCycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

        var effectiveCurrent = checked((uint)(currentCycles * activeCavities));
        ulong stepsUntilBoundary;

        if (currentTarget < standardVeTarget)
        {
            stepsUntilBoundary = 1;
        }
        else
        {
            var standardCycles = CeilingDivide(standardVeTarget, activeCavities);
            if (standardCycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
                throw new InvalidOperationException($"Die Standard-VE benötigt {standardCycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

            var effectiveStandard = standardCycles * activeCavities;
            stepsUntilBoundary = ((remaining - standardVeTarget) / effectiveStandard) + 1;
        }

        var boundary = (ulong)currentVeNumber + stepsUntilBoundary - 1;
        if (boundary > ModbusRegisterMap.MaxVeNumber)
            throw new InvalidOperationException($"Der Auftrag würde die LOGO!-VE-Grenze {ModbusRegisterMap.MaxVeNumber:N0} überschreiten. Auftrag aufteilen.");

        return new VeBoundaryPlan(
            currentTarget,
            (uint)currentCycles,
            effectiveCurrent,
            (ushort)boundary);
    }

    public static OrderCapacityPlan CalculateOrderCapacity(
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        if (orderTargetQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));
        if (standardVeTarget == 0)
            throw new ArgumentOutOfRangeException(nameof(standardVeTarget));
        if (activeCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(activeCavities));

        ulong remaining = orderTargetQuantity;
        ulong totalCycles = 0;
        uint ves = 0;

        while (remaining > 0)
        {
            var target = (uint)Math.Min((ulong)standardVeTarget, remaining);
            var cycles = CeilingDivide(target, activeCavities);
            if (cycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
                throw new InvalidOperationException($"Eine VE benötigt {cycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

            totalCycles += cycles;
            if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)
                throw new InvalidOperationException($"Der Auftrag benötigt {totalCycles:N0} LOGO!-Zyklen und überschreitet die freigegebene Grenze von {ModbusRegisterMap.MaxTotalCyclesPerJob:N0}. Auftrag segmentieren.");

            ves++;
            if (ves > ModbusRegisterMap.MaxVeNumber)
                throw new InvalidOperationException($"Der Auftrag benötigt mehr als {ModbusRegisterMap.MaxVeNumber:N0} VE. Auftrag aufteilen.");

            var effectiveQuantity = cycles * activeCavities;
            remaining = remaining > effectiveQuantity ? remaining - effectiveQuantity : 0;
        }

        return new OrderCapacityPlan((uint)totalCycles, (ushort)ves);
    }

    private static void ValidateBasicInputs(
        ushort currentVeNumber,
        uint producedBeforeCurrentVe,
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        if (currentVeNumber is 0 or > ModbusRegisterMap.MaxVeNumber)
            throw new ArgumentOutOfRangeException(nameof(currentVeNumber));
        if (orderTargetQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));
        if (standardVeTarget == 0)
            throw new ArgumentOutOfRangeException(nameof(standardVeTarget));
        if (activeCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(activeCavities));
        if (producedBeforeCurrentVe >= orderTargetQuantity)
            throw new ArgumentOutOfRangeException(nameof(producedBeforeCurrentVe), "Der Auftrag ist bereits vollständig produziert.");
    }

    private static ulong CeilingDivide(uint numerator, ushort denominator) =>
        ((ulong)numerator + denominator - 1UL) / denominator;
}
''')


# A job/config Ack is valid only if the LOGO has armed the safety boundary as well as echoing it.
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
old = '''        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)
            throw new InvalidOperationException($"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.");
        session.LastSnapshot = snapshot;'''
new = '''        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)
            throw new InvalidOperationException($"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.");
        if (expectedHoldAfterVeNumber is > 0 && (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) == 0)
            throw new InvalidOperationException($"{operation}: LOGO! hat HoldAfterVE {expectedHoldAfterVeNumber.Value} bestätigt, aber CompletionHoldArmed ist nicht aktiv.");
        session.LastSnapshot = snapshot;'''
text = replace_once(text, old, new, "CompletionHoldArmed Ack validation")
write(path, text)


# MainViewModel: check the local hold immediately, preserve VE history, and make a failed held-boundary
# reconfiguration recoverable through a safe Resume path that reconfigures before releasing the hold.
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)

new_resume = '''    private async Task ResumeSelectedOrderAsync()
    {
        var machine = SelectedMachine;
        if (machine is null || machine.OrderState != ProductionOrderState.Paused)
        {
            StatusMessage = "Die ausgewählte Maschine hat keinen pausierten Auftrag.";
            return;
        }

        if (machine.IsTemporarilyDisabled)
        {
            StatusMessage = "Maschine zuerst wieder aktivieren.";
            return;
        }

        try
        {
            if (!IsSimulationMode)
            {
                if (_scheduledCompletionHolds.TryGetValue(machine.Configuration.MachineNumber, out var priorHold) &&
                    priorHold > 0 && machine.CurrentVeNumber > priorHold)
                {
                    var diagnostics = _fleet.GetCommunicationDiagnostics(machine.Configuration.MachineNumber);
                    if (diagnostics is null ||
                        (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)
                        throw new InvalidOperationException("Grenzwiederanlauf gesperrt: LOGO! meldet keinen aktiven Completion-Hold.");

                    var recoveryPlan = VeBoundaryPolicy.Plan(
                        machine.CurrentVeNumber,
                        machine.OrderProducedQuantity,
                        machine.OrderTargetQuantity,
                        machine.TargetPartsPerVe,
                        machine.ActiveCavities);

                    var recoveryJob = new JobParameters(
                        StableUInt32(machine.OrderNumber),
                        machine.ArticleNumber,
                        machine.ToolNumber,
                        machine.ActiveCavities,
                        recoveryPlan.TargetParts,
                        recoveryPlan.TargetCycles,
                        ValvePulseMs,
                        recoveryPlan.HoldAfterVeNumber);

                    await _fleet.UpdateVeTargetAsync(machine.Configuration.MachineNumber, recoveryJob, pauseCounting: true);
                    _scheduledCompletionHolds[machine.Configuration.MachineNumber] = recoveryPlan.HoldAfterVeNumber;
                    await _database.AddEventAsync(machine.Configuration.MachineNumber, "VE_BOUNDARY_RECOVERY_CONFIGURED",
                        $"Grenzwiederanlauf vorbereitet: VE {machine.CurrentVeNumber}, Ziel {recoveryPlan.TargetParts}, nächster Hold {recoveryPlan.HoldAfterVeNumber}.");
                }

                await _fleet.ResumeCountingAsync(machine.Configuration.MachineNumber);
            }

            machine.ResumeOrder();
            StatusMessage = $"{machine.DisplayName}: Auftrag {machine.OrderNumber} fortgesetzt.";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_RESUMED", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag konnte nicht sicher fortgesetzt werden: {ex.Message}";
        }
    }

'''
text = replace_between(text,
    "    private async Task ResumeSelectedOrderAsync()\n",
    "    private async Task EndSelectedOrderAsync()\n",
    new_resume,
    "ResumeSelectedOrderAsync")

new_completion = '''    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        var completedUtc = e.CompletedAtLocal.ToUniversalTime();
        var targetForCompletedVe = Math.Max(1u, e.TargetQuantity);
        var overfill = e.Quantity > targetForCompletedVe ? e.Quantity - targetForCompletedVe : 0;
        var initialStatus = AutoPrintLabels ? "Pending" : "Disabled";
        var atHeldBoundary = false;
        string? boundaryError = null;

        if (!IsSimulationMode)
        {
            (atHeldBoundary, boundaryError) = await PrecheckRealVeBoundaryAsync(machine, e);
        }

        var record = new PackagingUnitRecord(
            $"PC-{completedUtc:yyyyMMddHHmmssfff}-M{machine.Configuration.MachineNumber:00}-VE{e.VeNumber:0000}",
            machine.Configuration.MachineNumber,
            machine.Configuration.Name,
            e.VeNumber,
            machine.OrderNumber,
            machine.ArticleNumber,
            machine.ArticleDescription,
            machine.ToolNumber,
            machine.ActiveCavities,
            targetForCompletedVe,
            e.Quantity,
            overfill,
            e.Reason,
            completedUtc,
            initialStatus,
            null);

        try
        {
            await _database.SavePackagingUnitAsync(record);

            if (AutoPrintLabels)
            {
                var printed = await _labelPrinter.PrintAsync(record, LabelPrinterName);
                var printedAt = printed ? DateTime.UtcNow : (DateTime?)null;
                var labelStatus = printed ? "Printed" : "PendingPrinter";
                await _database.UpdateLabelStatusAsync(record.Id, labelStatus, printedAt);
                record = record with { LabelStatus = labelStatus, PrintedAtUtc = printedAt };
            }

            RecentPackagingUnits.Insert(0, record);
            while (RecentPackagingUnits.Count > 100)
                RecentPackagingUnits.RemoveAt(RecentPackagingUnits.Count - 1);

            if (!string.IsNullOrWhiteSpace(boundaryError))
            {
                StatusMessage = $"SICHERHEITSHALT {machine.DisplayName}: {boundaryError} VE {e.VeNumber} wurde protokolliert; keine Zählfreigabe erteilt.";
                return;
            }

            if (!IsSimulationMode && atHeldBoundary)
                await ContinueAfterHeldBoundaryAsync(machine, e);

            StatusMessage = machine.OrderState == ProductionOrderState.Completed
                ? $"{machine.DisplayName}: VE {e.VeNumber} fertig; Auftrag {machine.OrderNumber} mit {machine.OrderProducedQuantity:N0} Teilen abgeschlossen und LOGO!-Grenzhalt aktiv."
                : $"{machine.DisplayName}: VE {e.VeNumber} fertig mit {e.Quantity:N0} Teilen; nächste VE {machine.CurrentVeTargetParts:N0} Teile; Etikett: {record.LabelStatus}.";
        }
        catch (Exception ex)
        {
            if (!IsSimulationMode && atHeldBoundary && machine.OrderState == ProductionOrderState.Running)
                machine.PauseOrder();
            StatusMessage = $"VE-Abschluss/Sicherheitsgrenze nicht vollständig verarbeitet: {ex.Message}";
        }
    }

    private async Task<(bool AtHeldBoundary, string? Error)> PrecheckRealVeBoundaryAsync(
        MachineState machine,
        VeCompletedEventArgs e)
    {
        var machineNumber = machine.Configuration.MachineNumber;
        if (!_scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduledHold) || scheduledHold == 0)
        {
            const string reason = "Für den aktiven Auftrag fehlt ein geplanter LOGO!-Grenzhalt.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        if (e.VeNumber < scheduledHold)
            return (false, null);

        if (e.VeNumber > scheduledHold)
        {
            var reason = $"VE {e.VeNumber} wurde abgeschlossen, obwohl der Grenzhalt nach VE {scheduledHold} geplant war.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        var diagnostics = _fleet.GetCommunicationDiagnostics(machineNumber);
        if (diagnostics is null ||
            (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) == 0)
        {
            var reason = $"LOGO! meldet an der geplanten Grenze VE {scheduledHold} keinen aktiven Completion-Hold.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            return (false, reason);
        }

        return (true, null);
    }

    private async Task ContinueAfterHeldBoundaryAsync(MachineState machine, VeCompletedEventArgs e)
    {
        var machineNumber = machine.Configuration.MachineNumber;

        if (machine.OrderState == ProductionOrderState.Completed)
        {
            await _fleet.PauseCountingAsync(machineNumber);
            _scheduledCompletionHolds.Remove(machineNumber);
            await _database.AddEventAsync(machineNumber, "VE_BOUNDARY_HOLD_FINAL",
                $"Auftrag {machine.OrderNumber}: finaler Grenzhalt nach VE {e.VeNumber} aktiv und Zählpause bestätigt.");
            return;
        }

        if (!machine.IsActiveOrder || machine.CurrentVeTargetParts == 0)
        {
            const string reason = "Nach Grenzhalt ist kein gültiges nächstes VE-Ziel vorhanden.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            throw new InvalidOperationException(reason);
        }

        VeBoundaryPlan nextPlan;
        try
        {
            nextPlan = VeBoundaryPolicy.Plan(
                machine.CurrentVeNumber,
                machine.OrderProducedQuantity,
                machine.OrderTargetQuantity,
                machine.TargetPartsPerVe,
                machine.ActiveCavities);
        }
        catch (Exception ex)
        {
            await EnterBoundaryFailSafeAsync(machine, $"Nächste VE konnte nicht sicher geplant werden: {ex.Message}");
            throw;
        }

        if (nextPlan.TargetParts != machine.CurrentVeTargetParts)
        {
            var reason = $"Planungsabweichung: MachineState erwartet {machine.CurrentVeTargetParts}, Grenzplan {nextPlan.TargetParts} Teile.";
            await EnterBoundaryFailSafeAsync(machine, reason);
            throw new InvalidOperationException(reason);
        }

        var nextJob = new JobParameters(
            StableUInt32(machine.OrderNumber),
            machine.ArticleNumber,
            machine.ToolNumber,
            machine.ActiveCavities,
            nextPlan.TargetParts,
            nextPlan.TargetCycles,
            ValvePulseMs,
            nextPlan.HoldAfterVeNumber);

        try
        {
            await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);
            _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;
            if (machine.OrderState == ProductionOrderState.Running)
                await _fleet.ResumeCountingAsync(machineNumber);
        }
        catch (Exception ex)
        {
            await EnterBoundaryFailSafeAsync(machine, $"Grenz-Rekonfiguration fehlgeschlagen: {ex.Message}");
            throw;
        }

        await _database.AddEventAsync(machineNumber, "VE_BOUNDARY_RECONFIGURED",
            $"Nach VE {e.VeNumber}: neues Ziel {nextPlan.TargetParts} Teile; nächster sicherer Grenzhalt VE {nextPlan.HoldAfterVeNumber}.");
    }

    private async Task EnterBoundaryFailSafeAsync(MachineState machine, string reason)
    {
        var detail = reason;
        try
        {
            await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);
        }
        catch (Exception pauseError)
        {
            detail += $" Zusätzlicher Pause-Befehl fehlgeschlagen: {pauseError.Message}";
        }

        if (machine.OrderState == ProductionOrderState.Running)
            machine.PauseOrder();

        await _database.AddEventAsync(machine.Configuration.MachineNumber, "SAFETY_VE_BOUNDARY_STOP", detail);
        StatusMessage = $"SICHERHEITSHALT {machine.DisplayName}: {detail}";
    }

'''
text = replace_between(text,
    "    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)\n",
    "    private void MachineOnPropertyChanged(object? sender, PropertyChangedEventArgs e)\n",
    new_completion,
    "MachineOnVeCompleted refined boundary")
write(path, text)


# Extend deterministic tests with total-cycle capacity and exact upper edge.
path = "tests/Partcounter.Tests/VeBoundaryPolicyTests.cs"
text = read(path)
insert = '''
    [Fact]
    public void LogoTotalCycleLimit_IsRejectedBeforeOrderStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VeBoundaryPolicy.Plan(1, 0, 1_000_000, 1000, 1));
        Assert.Contains("999.999", ex.Message);
    }

    [Fact]
    public void ExactLogoTotalCycleLimit_IsAccepted()
    {
        var capacity = VeBoundaryPolicy.CalculateOrderCapacity(999_999, 1000, 1);
        Assert.Equal((uint)999_999, capacity.RequiredTotalCycles);
        Assert.Equal((ushort)1000, capacity.RequiredPackagingUnits);
    }
'''
marker = "\n    [Fact]\n    public void OrdersBeyondLogoVeRange_AreRejectedBeforeStart()"
if marker not in text:
    raise SystemExit("Test insertion marker not found")
text = text.replace(marker, insert + marker, 1)
write(path, text)

print("R001.25 boundary refinements prepared successfully.")
