from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8", newline="\n")


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


# 1) Remove the single stale UI fallback revision string found by the industrial gate.
path = "src/Partcounter.App/MainWindow.xaml"
text = read(path)
text = replace_once(text, 'Title="Partcounter R001.5"', 'Title="Partcounter"', "MainWindow stale title")
write(path, text)


# 2) Protocol V3: additive registers only. Existing V2 offsets remain untouched.
path = "src/Partcounter.App/Services/ModbusRegisterMap.cs"
text = read(path)
text = replace_once(text, "public const ushort ConfigLength = 12;", "public const ushort ConfigLength = 13;", "ConfigLength")
text = replace_once(text,
    "    public const int ConfigPcHeartbeat = 11;\n",
    "    public const int ConfigPcHeartbeat = 11;\n    public const int ConfigHoldAfterVeNumber = 12;\n",
    "ConfigHoldAfterVeNumber")
text = replace_once(text, "public const ushort StatusLength = 18;", "public const ushort StatusLength = 19;", "StatusLength")
text = replace_once(text,
    "    public const int StatusLastCompletedCavities = 17;\n",
    "    public const int StatusLastCompletedCavities = 17;\n    public const int StatusHoldAfterVeNumberEcho = 18;\n",
    "StatusHoldAfterVeNumberEcho")
text = replace_once(text, "public const ushort ProtocolVersion = 2;", "public const ushort ProtocolVersion = 3;", "ProtocolVersion")
text = replace_once(text,
    "    public const ushort MaxHeartbeatValue = 32_767;\n",
    "    public const ushort MaxHeartbeatValue = 32_767;\n    public const ushort MaxVeNumber = 32_767;\n",
    "MaxVeNumber")
text = replace_once(text,
    "    public const ushort StatusPcHeartbeatStale = 1 << 5;\n",
    "    public const ushort StatusPcHeartbeatStale = 1 << 5;\n    public const ushort StatusCompletionHoldArmed = 1 << 6;\n    public const ushort StatusCompletionHoldActive = 1 << 7;\n",
    "completion hold status bits")
write(path, text)


# 3) Extend the immutable protocol records.
path = "src/Partcounter.App/Models/PartcounterModels.cs"
text = read(path)
text = replace_once(text,
    "    uint TargetCyclesPerVe,\n    ushort ValvePulseMs = 750);",
    "    uint TargetCyclesPerVe,\n    ushort ValvePulseMs = 750,\n    ushort HoldAfterVeNumber = 0);",
    "JobParameters HoldAfterVeNumber")
text = replace_once(text,
    "    VeCompletionReason LastCompletionReason,\n    DateTime ReadAtUtc);",
    "    VeCompletionReason LastCompletionReason,\n    DateTime ReadAtUtc,\n    ushort HoldAfterVeNumberEcho = 0);",
    "LogoSnapshot HoldAfterVeNumberEcho")
write(path, text)


# 4) Deterministic planner: first VE at which the LOGO must stop locally.
write("src/Partcounter.App/Services/VeBoundaryPolicy.cs", '''namespace Partcounter.Services;

public sealed record VeBoundaryPlan(
    uint TargetParts,
    uint TargetCycles,
    uint EffectiveQuantity,
    ushort HoldAfterVeNumber);

public static class VeBoundaryPolicy
{
    public static VeBoundaryPlan Plan(
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

    private static ulong CeilingDivide(uint numerator, ushort denominator) =>
        ((ulong)numerator + denominator - 1UL) / denominator;
}
''')


# 5) Modbus client sends/reads the new boundary and validates its range.
path = "src/Partcounter.App/Services/LogoModbusClient.cs"
text = read(path)
text = replace_once(text,
    "            ModbusRegisterMap.LowWord(job.TargetCyclesPerVe),\n            0\n        ];",
    "            ModbusRegisterMap.LowWord(job.TargetCyclesPerVe),\n            0,\n            job.HoldAfterVeNumber\n        ];",
    "WriteJob hold register")
text = replace_once(text,
    "            (VeCompletionReason)registers[ModbusRegisterMap.StatusLastCompletionReason],\n            DateTime.UtcNow);",
    "            (VeCompletionReason)registers[ModbusRegisterMap.StatusLastCompletionReason],\n            DateTime.UtcNow,\n            registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho]);",
    "ReadSnapshot hold echo")
text = replace_once(text,
    "LOGO! reported a VE cycle counter outside the Partcounter V2 range.",
    "LOGO! reported a VE cycle counter outside the Partcounter V3 range.",
    "V3 counter message")
text = replace_once(text,
    "        if (job.ValvePulseMs % ModbusRegisterMap.ValvePulseUnitMs != 0)\n            throw new ArgumentOutOfRangeException(nameof(job), $\"Valve pulse must be a multiple of {ModbusRegisterMap.ValvePulseUnitMs} ms.\");\n",
    "        if (job.ValvePulseMs % ModbusRegisterMap.ValvePulseUnitMs != 0)\n            throw new ArgumentOutOfRangeException(nameof(job), $\"Valve pulse must be a multiple of {ModbusRegisterMap.ValvePulseUnitMs} ms.\");\n\n        if (job.HoldAfterVeNumber > ModbusRegisterMap.MaxVeNumber)\n            throw new ArgumentOutOfRangeException(nameof(job), $\"Hold-after VE must be 0..{ModbusRegisterMap.MaxVeNumber:N0}.\");\n",
    "HoldAfterVE validation")
write(path, text)


# 6) Ack validation includes the safety-boundary echo for job/config writes.
path = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(path)
text = replace_once(text,
    '''                $"Auftrag an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken);''',
    '''                $"Auftrag an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken,
                job.HoldAfterVeNumber);''',
    "SendJob expected hold")
text = replace_once(text,
    '''                $"VE-Zielupdate an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken);''',
    '''                $"VE-Zielupdate an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken,
                job.HoldAfterVeNumber);''',
    "UpdateTarget expected hold")
text = replace_once(text,
    '''        ushort? expectedCavities,
        CancellationToken cancellationToken)
    {''',
    '''        ushort? expectedCavities,
        CancellationToken cancellationToken,
        ushort? expectedHoldAfterVeNumber = null)
    {''',
    "ExecuteConfirmed signature")
text = replace_once(text,
    "return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities);",
    "return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);",
    "pre-send ValidateAcknowledgement")
text = replace_once(text,
    "return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken);",
    "return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber);",
    "Wait ack forwarding")
text = replace_once(text,
    '''        ushort? expectedCavities,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();''',
    '''        ushort? expectedCavities,
        CancellationToken cancellationToken,
        ushort? expectedHoldAfterVeNumber = null)
    {
        var stopwatch = Stopwatch.StartNew();''',
    "WaitForCommandAcknowledgement signature")
text = replace_once(text,
    "var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities);",
    "var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);",
    "wait ack validation")
text = replace_once(text,
    '''        string operation,
        ushort? expectedCavities)
    {''',
    '''        string operation,
        ushort? expectedCavities,
        ushort? expectedHoldAfterVeNumber = null)
    {''',
    "ValidateAcknowledgement signature")
text = replace_once(text,
    '''        if (expectedCavities.HasValue && snapshot.ActiveCavitiesEcho != expectedCavities.Value)
            throw new InvalidOperationException($"{operation}: Kavitäten-Echo {snapshot.ActiveCavitiesEcho} entspricht nicht Soll {expectedCavities.Value}.");
        session.LastSnapshot = snapshot;''',
    '''        if (expectedCavities.HasValue && snapshot.ActiveCavitiesEcho != expectedCavities.Value)
            throw new InvalidOperationException($"{operation}: Kavitäten-Echo {snapshot.ActiveCavitiesEcho} entspricht nicht Soll {expectedCavities.Value}.");
        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)
            throw new InvalidOperationException($"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.");
        session.LastSnapshot = snapshot;''',
    "HoldAfterVE ack echo")
text = text.replace("Partcounter V2 range.", "Partcounter V3 range.")
write(path, text)


# 7) MainViewModel schedules the first boundary in advance and only reconfigures while LOGO is locally held.
path = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(path)
text = replace_once(text,
    "    private readonly DispatcherTimer _simulationTimer;\n",
    "    private readonly DispatcherTimer _simulationTimer;\n    private readonly Dictionary<int, ushort> _scheduledCompletionHolds = new();\n",
    "scheduled hold dictionary")

new_toggle = '''    private async Task ToggleOperatingModeAsync()
    {
        if (Machines.Any(m => m.IsActiveOrder))
        {
            StatusMessage = "Betriebsartwechsel gesperrt: Laufende oder pausierte Aufträge zuerst kontrolliert beenden. Simulation und Echtbetrieb dürfen keinen unterschiedlichen Auftragszustand übernehmen.";
            return;
        }

        if (IsSimulationMode)
        {
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Offline;

            await _fleet.StartAsync(Machines.Select(m => m.Configuration));
            foreach (var machine in Machines.Where(m => m.IsTemporarilyDisabled))
                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);

            IsSimulationMode = false;
            StatusMessage = "Echtbetrieb aktiv: Partcounter verbindet parallel mit allen freigegebenen LOGO!-Stationen. Protocol V3 wird zwingend geprüft.";
        }
        else
        {
            await _fleet.StopAsync();
            IsSimulationMode = true;
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Simulation;
            StatusMessage = "Simulation aktiv. Es werden keine Modbus-Schreibbefehle an LOGO! gesendet.";
        }
    }

'''
text = replace_between(text,
    "    private async Task ToggleOperatingModeAsync()\n",
    "    private async Task ApplySelectedArticleAsync()\n",
    new_toggle,
    "ToggleOperatingModeAsync")

new_apply = '''    private async Task ApplySelectedArticleAsync()
    {
        if (SelectedMachine is null || SelectedArticle is null)
        {
            StatusMessage = "Bitte Maschine und Artikel auswählen.";
            return;
        }

        if (SelectedMachine.IsActiveOrder)
        {
            StatusMessage = $"{SelectedMachine.DisplayName}: Es läuft bereits ein Auftrag. Bitte zuerst pausieren/beenden.";
            return;
        }

        if (SelectedArticle.ActiveCavities is < 1 or > 64 || SelectedArticle.PackagingQuantity == 0)
        {
            StatusMessage = "Artikelparameter sind ungültig.";
            return;
        }

        if (OrderTargetQuantity == 0)
        {
            StatusMessage = "Die Auftragsmenge muss größer als 0 sein.";
            return;
        }

        var machine = SelectedMachine;
        var article = SelectedArticle;
        var order = string.IsNullOrWhiteSpace(OrderNumber)
            ? $"AUF-{DateTime.Now:yyyyMMdd-HHmmss}"
            : OrderNumber.Trim();

        VeBoundaryPlan firstPlan;
        try
        {
            firstPlan = VeBoundaryPolicy.Plan(1, 0, OrderTargetQuantity, article.PackagingQuantity, article.ActiveCavities);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auftrag kann nicht sicher geplant werden: {ex.Message}";
            return;
        }

        var wasTemporarilyDisabled = machine.IsTemporarilyDisabled;

        if (!IsSimulationMode)
        {
            if (wasTemporarilyDisabled)
                await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);

            var job = new JobParameters(
                StableUInt32(order),
                article.ArticleNumber,
                article.ToolNumber,
                article.ActiveCavities,
                firstPlan.TargetParts,
                firstPlan.TargetCycles,
                ValvePulseMs,
                firstPlan.HoldAfterVeNumber);

            try
            {
                await _fleet.SendJobAsync(machine.Configuration.MachineNumber, job);
            }
            catch (Exception ex)
            {
                if (wasTemporarilyDisabled)
                {
                    try
                    {
                        await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                    }
                    catch
                    {
                    }
                }

                StatusMessage = $"Auftrag nicht übernommen – LOGO!-Protocol-V3-Übertragung fehlgeschlagen: {ex.Message}";
                await _database.AddEventAsync(machine.Configuration.MachineNumber, "MODBUS_WRITE_ERROR", ex.Message);
                return;
            }
        }

        machine.StartOrder(article, order, OrderTargetQuantity);
        _scheduledCompletionHolds[machine.Configuration.MachineNumber] = firstPlan.HoldAfterVeNumber;
        SelectedMachine = machine;

        StatusMessage =
            $"{machine.DisplayName}: Auftrag {order} gestartet · Soll {OrderTargetQuantity:N0} Teile · " +
            $"{machine.RequiredOrderVes:N0} VE geplant · erste VE {machine.CurrentVeTargetParts:N0} Teile · " +
            $"sicherer LOGO!-Grenzhalt nach VE {firstPlan.HoldAfterVeNumber:N0}.";
        await _database.AddEventAsync(machine.Configuration.MachineNumber, "JOB_STARTED", StatusMessage);
    }

'''
text = replace_between(text,
    "    private async Task ApplySelectedArticleAsync()\n",
    "    private async Task PauseSelectedOrderAsync()\n",
    new_apply,
    "ApplySelectedArticleAsync")

text = replace_once(text,
    "            machine.EndOrder();\n            StatusMessage =",
    "            machine.EndOrder();\n            _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);\n            StatusMessage =",
    "EndOrder hold cleanup")

new_reset = '''    private async Task ResetMachineAsync(object? parameter)
    {
        if (parameter is not MachineState machine) return;

        if (!machine.IsActiveOrder)
        {
            StatusMessage = $"{machine.DisplayName}: Kein aktiver Auftrag zum Zurücksetzen.";
            return;
        }

        VeBoundaryPlan resetPlan;
        try
        {
            resetPlan = VeBoundaryPolicy.Plan(1, 0, machine.OrderTargetQuantity, machine.TargetPartsPerVe, machine.ActiveCavities);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset kann nicht sicher geplant werden: {ex.Message}";
            return;
        }

        if (IsSimulationMode)
        {
            machine.ResetCounters();
            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;
            StatusMessage = $"{machine.DisplayName}: Auftragszähler zurückgesetzt.";
            return;
        }

        try
        {
            var resetJob = new JobParameters(
                StableUInt32(machine.OrderNumber),
                machine.ArticleNumber,
                machine.ToolNumber,
                machine.ActiveCavities,
                resetPlan.TargetParts,
                resetPlan.TargetCycles,
                ValvePulseMs,
                resetPlan.HoldAfterVeNumber);

            await _fleet.SendJobAsync(machine.Configuration.MachineNumber, resetJob);
            machine.ResetCounters();
            _scheduledCompletionHolds[machine.Configuration.MachineNumber] = resetPlan.HoldAfterVeNumber;
            StatusMessage = $"Reset an {machine.DisplayName} gesendet; Grenzhalt nach VE {resetPlan.HoldAfterVeNumber:N0} bestätigt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset fehlgeschlagen: {ex.Message}";
        }
    }

'''
text = replace_between(text,
    "    private async Task ResetMachineAsync(object? parameter)\n",
    "    private async Task SaveArticleAsync()\n",
    new_reset,
    "ResetMachineAsync")

new_completion = '''    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        var completedUtc = e.CompletedAtLocal.ToUniversalTime();
        var targetForCompletedVe = Math.Max(1u, e.TargetQuantity);
        var overfill = e.Quantity > targetForCompletedVe ? e.Quantity - targetForCompletedVe : 0;
        var initialStatus = AutoPrintLabels ? "Pending" : "Disabled";

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

            if (!IsSimulationMode)
                await HandleRealVeBoundaryAsync(machine, e);

            StatusMessage = machine.OrderState == ProductionOrderState.Completed
                ? $"{machine.DisplayName}: VE {e.VeNumber} fertig; Auftrag {machine.OrderNumber} mit {machine.OrderProducedQuantity:N0} Teilen abgeschlossen und LOGO!-Grenzhalt aktiv."
                : $"{machine.DisplayName}: VE {e.VeNumber} fertig mit {e.Quantity:N0} Teilen; nächste VE {machine.CurrentVeTargetParts:N0} Teile; Etikett: {record.LabelStatus}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"VE-Abschluss/Sicherheitsgrenze nicht vollständig verarbeitet: {ex.Message}";
        }
    }

    private async Task HandleRealVeBoundaryAsync(MachineState machine, VeCompletedEventArgs e)
    {
        var machineNumber = machine.Configuration.MachineNumber;
        if (!_scheduledCompletionHolds.TryGetValue(machineNumber, out var scheduledHold) || scheduledHold == 0)
        {
            await EnterBoundaryFailSafeAsync(machine, "Für den aktiven Auftrag fehlt ein geplanter LOGO!-Grenzhalt.");
            throw new InvalidOperationException("Fehlende HoldAfterVE-Planung; Zählung wurde sicherheitshalber pausiert.");
        }

        if (e.VeNumber < scheduledHold)
            return;

        if (e.VeNumber > scheduledHold)
        {
            await EnterBoundaryFailSafeAsync(machine, $"VE {e.VeNumber} wurde abgeschlossen, obwohl der Grenzhalt nach VE {scheduledHold} geplant war.");
            throw new InvalidOperationException("Geplanter VE-Grenzhalt wurde überschritten.");
        }

        var diagnostics = _fleet.GetCommunicationDiagnostics(machineNumber);
        var holdActive = diagnostics is not null &&
                         (diagnostics.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0;
        if (!holdActive)
        {
            await EnterBoundaryFailSafeAsync(machine, $"LOGO! meldet an der geplanten Grenze VE {scheduledHold} keinen aktiven Completion-Hold.");
            throw new InvalidOperationException("LOGO!-Completion-Hold fehlt an der Sicherheitsgrenze.");
        }

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
            await EnterBoundaryFailSafeAsync(machine, "Nach Grenzhalt ist kein gültiges nächstes VE-Ziel vorhanden.");
            throw new InvalidOperationException("Nächstes VE-Ziel fehlt.");
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
            await EnterBoundaryFailSafeAsync(machine,
                $"Planungsabweichung: MachineState erwartet {machine.CurrentVeTargetParts}, Grenzplan {nextPlan.TargetParts} Teile.");
            throw new InvalidOperationException("VE-Zielberechnung ist inkonsistent.");
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

        await _fleet.UpdateVeTargetAsync(machineNumber, nextJob, pauseCounting: true);
        _scheduledCompletionHolds[machineNumber] = nextPlan.HoldAfterVeNumber;
        if (machine.OrderState == ProductionOrderState.Running)
            await _fleet.ResumeCountingAsync(machineNumber);

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

        await _database.AddEventAsync(machine.Configuration.MachineNumber, "SAFETY_VE_BOUNDARY_STOP", detail);
        StatusMessage = $"SICHERHEITSHALT {machine.DisplayName}: {detail}";
    }

'''
text = replace_between(text,
    "    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)\n",
    "    private void MachineOnPropertyChanged(object? sender, PropertyChangedEventArgs e)\n",
    new_completion,
    "MachineOnVeCompleted/Boundary")
write(path, text)


# 8) Regression contract + deterministic planner tests.
path = "tests/Partcounter.Tests/CoreRegressionTests.cs"
text = read(path)
text = replace_once(text,
    '''    public void ProtocolV2_ContractRemainsStable()
    {
        Assert.Equal((ushort)2, ModbusRegisterMap.ProtocolVersion);
        Assert.Equal((ushort)0, ModbusRegisterMap.ConfigStart);
        Assert.Equal((ushort)12, ModbusRegisterMap.ConfigLength);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusStart);
        Assert.Equal((ushort)18, ModbusRegisterMap.StatusLength);
        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);
    }''',
    '''    public void ProtocolV3_ContractExtendsBuffersWithoutMovingExistingAddresses()
    {
        Assert.Equal((ushort)3, ModbusRegisterMap.ProtocolVersion);
        Assert.Equal((ushort)0, ModbusRegisterMap.ConfigStart);
        Assert.Equal((ushort)13, ModbusRegisterMap.ConfigLength);
        Assert.Equal(12, ModbusRegisterMap.ConfigHoldAfterVeNumber);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusStart);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusLength);
        Assert.Equal(18, ModbusRegisterMap.StatusHoldAfterVeNumberEcho);
        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);
    }''',
    "Protocol V3 contract test")
write(path, text)

write("tests/Partcounter.Tests/VeBoundaryPolicyTests.cs", '''using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class VeBoundaryPolicyTests
{
    [Fact]
    public void ExactTwoVeOrder_HoldsAtFinalVe()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2000, 1000, 1);
        Assert.Equal((uint)1000, plan.TargetParts);
        Assert.Equal((uint)1000, plan.TargetCycles);
        Assert.Equal((ushort)2, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void SixtyFourCavities_SchedulesHoldBeforePartialVe()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2500, 1000, 64);
        Assert.Equal((uint)1000, plan.TargetParts);
        Assert.Equal((uint)16, plan.TargetCycles);
        Assert.Equal((uint)1024, plan.EffectiveQuantity);
        Assert.Equal((ushort)2, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void PartialFinalVe_IsHeldAtItsOwnCompletion()
    {
        var plan = VeBoundaryPolicy.Plan(3, 2048, 2500, 1000, 64);
        Assert.Equal((uint)452, plan.TargetParts);
        Assert.Equal((uint)8, plan.TargetCycles);
        Assert.Equal((uint)512, plan.EffectiveQuantity);
        Assert.Equal((ushort)3, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void LongUniformOrder_RemainsAutonomousUntilRealBoundary()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 10000, 1000, 8);
        Assert.Equal((ushort)10, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void CavityOverfill_CanMoveBoundaryEarlier()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2000, 1000, 64);
        Assert.Equal((ushort)1, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void OrdersBeyondLogoVeRange_AreRejectedBeforeStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VeBoundaryPolicy.Plan(1, 0, 40000, 1, 1));
        Assert.Contains("Auftrag aufteilen", ex.Message);
    }
}
''')

print("R001.25 Protocol V3 source/test patch prepared successfully.")
