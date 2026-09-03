using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Partcounter.Models;
using Partcounter.ViewModels;

namespace Partcounter.Services;

/// <summary>
/// Headless/CI stress run against the real WPF application.
/// HF5 verifies not only load/stability but also the functional contract:
/// simulation must stay in-memory, must start orders normally and must remain
/// completely isolated from the live MachineState/fleet/recovery state.
/// </summary>
public sealed class ApplicationStressService
{
    public async Task<int> RunAsync(MainWindow window, string reportPath, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();
        var process = Process.GetCurrentProcess();
        var peakWorkingSet = process.WorkingSet64;
        var peakManaged = GC.GetTotalMemory(false);
        var stressVeEvents = 0;
        var errors = new List<string>();
        var notes = new List<string>();

        try
        {
            await WaitUntilAsync(
                () => window.DataContext is MainViewModel vm &&
                      vm.Hf5IsolationEnabled &&
                      vm.Hf5IsUsingSimulationMachines &&
                      vm.Machines.Count == 30 &&
                      vm.Articles.Count > 0,
                TimeSpan.FromSeconds(60),
                cancellationToken);

            if (window.DataContext is not MainViewModel vm)
                throw new InvalidOperationException("MainViewModel wurde im Stresslauf nicht gefunden.");
            if (!vm.IsSimulationMode || !vm.Hf5IsUsingSimulationMachines)
                throw new InvalidOperationException("HF5-Stresslauf muss mit dem isolierten Simulations-Maschinensatz starten.");

            vm.AutoPrintLabels = false;
            var article = vm.Articles.FirstOrDefault(a => a.ActiveCavities == 64)
                ?? vm.Articles.First();

            var databasePath = new DatabaseService().DatabasePath;
            var persistedBefore = await CountStressRowsAsync(databasePath, cancellationToken);

            // Bedienpfad prüfen: Der echte Auftrag-starten-Command muss im Simulationsmodus
            // trotz eventuell geparkter Echtbetriebs-Recoverydaten funktionieren.
            var commandMachine = vm.Machines.First();
            vm.SelectedMachine = commandMachine;
            vm.SelectedArticle = article;
            vm.OrderNumber = "STRESS-COMMAND-M01";
            vm.OrderTargetQuantity = 4096;
            vm.ApplyArticleCommand.Execute(null);
            await WaitUntilAsync(
                () => commandMachine.OrderState == ProductionOrderState.Running,
                TimeSpan.FromSeconds(10),
                cancellationToken);
            notes.Add("Simulations-Auftrag über regulären ApplyArticleCommand: PASS");
            commandMachine.EndOrder();

            foreach (var machine in vm.Machines)
                machine.VeCompleted += CountVe;

            notes.Add($"Maschinen: {vm.Machines.Count}");
            notes.Add($"Stressartikel: {article.ArticleNumber}, Kavitäten={article.ActiveCavities}, VE={article.PackagingQuantity}");

            const int rounds = 4;
            const uint targetPerRound = 16_000;
            notes.Add($"Simulierte Sollteile: {(long)rounds * targetPerRound * vm.Machines.Count:N0}");

            for (var round = 1; round <= rounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var machine in vm.Machines)
                {
                    machine.StartOrder(article, $"STRESS-R{round:00}-M{machine.Configuration.MachineNumber:00}", targetPerRound);
                    machine.PauseOrder();
                    machine.ResumeOrder();
                    machine.SetTemporarilyDisabled(true);
                    machine.SetTemporarilyDisabled(false);
                    machine.ResumeOrder();
                }

                var cavityCount = Math.Max(1, (int)article.ActiveCavities);
                var maxCycles = (int)Math.Ceiling(targetPerRound / (double)cavityCount) + 32;
                for (var cycle = 0; cycle < maxCycles; cycle++)
                {
                    foreach (var machine in vm.Machines)
                    {
                        if (machine.OrderState == ProductionOrderState.Running)
                            machine.ApplySimulationCycle();
                    }

                    if (cycle % 20 == 0)
                    {
                        process.Refresh();
                        peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                        peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        await Task.Delay(2, cancellationToken);
                    }
                }

                var unfinished = vm.Machines.Where(m => m.OrderState == ProductionOrderState.Running).ToList();
                if (unfinished.Count > 0)
                    errors.Add($"Runde {round}: {unfinished.Count} Maschinen nach geplantem Zyklusbudget noch nicht abgeschlossen.");

                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                notes.Add($"Runde {round}: VE-Ereignisse kumuliert={Volatile.Read(ref stressVeEvents):N0}");
            }

            foreach (var machine in vm.Machines)
                machine.VeCompleted -= CountVe;

            var expected = Volatile.Read(ref stressVeEvents);

            // HF5-Fachvertrag: keine Simulations-VE in PackagingUnits. Frühere Builds
            // verlangten fälschlich das Gegenteil und konnten dadurch Vermischungen übersehen.
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(250, cancellationToken);
            var persistedAfter = await CountStressRowsAsync(databasePath, cancellationToken);
            if (persistedAfter != persistedBefore)
            {
                errors.Add(
                    $"HF5-Isolationsbruch: Simulations-VE haben PackagingUnits verändert. " +
                    $"Vorher={persistedBefore:N0}, nachher={persistedAfter:N0}.");
            }
            else
            {
                notes.Add("Simulation → Produktionshistorie: 0 neue Datensätze (PASS)");
            }

            // Zustandswechsel prüfen. Der Simulationszustand muss beim Wechsel auf den
            // separaten Live-Satz eingefroren bleiben und danach exakt wieder erscheinen.
            var simulationMachine01 = vm.Machines.First(m => m.Configuration.MachineNumber == 1);
            var simulationOrderState = simulationMachine01.OrderState;
            var simulationTotalCycles = simulationMachine01.TotalCycles;
            var simulationCompletedVes = simulationMachine01.CompletedVes;
            var machine01HadParkedLiveRecovery = vm.Hf5HasParkedLiveRecoveryForValidation(1);

            vm.Hf5ToggleOperatingModeCommand.Execute(null);
            await WaitUntilAsync(
                () => !vm.IsSimulationMode && vm.Hf5IsUsingLiveMachines,
                TimeSpan.FromSeconds(20),
                cancellationToken);
            await WaitUntilAsync(
                () => vm.Hf5ToggleOperatingModeCommand.CanExecute(null),
                TimeSpan.FromSeconds(30),
                cancellationToken);

            var liveMachine01 = vm.Machines.First(m => m.Configuration.MachineNumber == 1);
            if (ReferenceEquals(simulationMachine01, liveMachine01))
                errors.Add("HF5-Isolationsbruch: M01 verwendet in Simulation und Echtbetrieb dieselbe MachineState-Instanz.");
            if (liveMachine01.OrderState != ProductionOrderState.None && !machine01HadParkedLiveRecovery)
                errors.Add($"HF5-Isolationsbruch: frischer Live-M01 übernahm Simulations-Auftragszustand {liveMachine01.OrderState}.");
            else if (machine01HadParkedLiveRecovery)
                notes.Add($"Vorhandener M01-Echtbetrieb-Recoveryzustand blieb separat erhalten ({liveMachine01.OrderState}).");
            notes.Add("Simulation → Echtbetrieb: separate MachineState-Instanz geprüft");

            vm.Hf5ToggleOperatingModeCommand.Execute(null);
            await WaitUntilAsync(
                () => vm.IsSimulationMode && vm.Hf5IsUsingSimulationMachines,
                TimeSpan.FromSeconds(20),
                cancellationToken);

            var restoredSimulationMachine01 = vm.Machines.First(m => m.Configuration.MachineNumber == 1);
            if (!ReferenceEquals(simulationMachine01, restoredSimulationMachine01))
                errors.Add("HF5-Isolationsbruch: Rückkehr zur Simulation stellte nicht dieselbe Simulationsinstanz wieder her.");
            if (restoredSimulationMachine01.OrderState != simulationOrderState ||
                restoredSimulationMachine01.TotalCycles != simulationTotalCycles ||
                restoredSimulationMachine01.CompletedVes != simulationCompletedVes)
            {
                errors.Add("HF5-Isolationsbruch: Simulationszustand wurde beim Live-Wechsel verändert.");
            }
            else
            {
                notes.Add("Echtbetrieb → Simulation: eingefrorener Simulationszustand unverändert wiederhergestellt (PASS)");
            }

            var health = await new ProductionReadinessService().CheckDatabaseAsync();
            if (!health.IsOk)
                errors.Add($"SQLite-Integritätsprüfung fehlgeschlagen: {health.Summary}");

            var database = new DatabaseService();
            for (var i = 0; i < 150; i++)
            {
                _ = await database.LoadRecentPackagingUnitsAsync(100);
                _ = await database.LoadArticlesAsync();
                if (i % 25 == 0)
                    await database.AddEventAsync(null, "STRESS_HEARTBEAT", $"Stress-Lesedurchlauf {i}");
            }

            await StressOrderInterfacesAsync(notes, errors, cancellationToken);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.PeakWorkingSet64);
            peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));

            notes.Add($"VE-Ereignisse gesamt: {expected:N0}");
            notes.Add($"Persistierte Simulations-Stress-VE neu: {persistedAfter - persistedBefore:N0} (Soll 0)");
            notes.Add($"SQLite quick_check: {health.QuickCheck}");
            notes.Add($"Working Set Ende: {process.WorkingSet64 / 1024d / 1024d:N1} MiB");
            notes.Add($"Peak Working Set: {peakWorkingSet / 1024d / 1024d:N1} MiB");
            notes.Add($"Peak Managed Memory: {peakManaged / 1024d / 1024d:N1} MiB");

            await WriteReportAsync(reportPath, errors.Count == 0, started.Elapsed, notes, errors, cancellationToken);
            return errors.Count == 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            errors.Add(ex.ToString());
            await WriteReportAsync(reportPath, false, started.Elapsed, notes, errors, CancellationToken.None);
            return 3;
        }

        void CountVe(object? sender, VeCompletedEventArgs args) => Interlocked.Increment(ref stressVeEvents);
    }

    private static async Task StressOrderInterfacesAsync(List<string> notes, List<string> errors, CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), $"Partcounter_R00125_HF5_Stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "orders.csv");
        try
        {
            const int orderCount = 5000;
            var sb = new StringBuilder(orderCount * 100);
            sb.AppendLine("MachineNumber;MachineName;MachineId;WorkCenter;OrderNumber;OperationNumber;ArticleNumber;OrderQuantity;ArticleDescription;ToolNumber;Cavities;PackagingQuantity;PlannedStart;OrderStatus;Company;Plant");
            for (var i = 1; i <= orderCount; i++)
            {
                var machine = ((i - 1) % 30) + 1;
                sb.Append(machine).Append(';')
                  .Append($"Spritzgussmaschine {machine:00}").Append(';')
                  .Append($"ARB-{machine:00}").Append(';')
                  .Append($"SGM-{machine:00}").Append(';')
                  .Append($"STRESS-ERP-{i:000000}").Append(';')
                  .Append("0010;")
                  .Append($"ART-{i % 400:000}").Append(';')
                  .Append(1000 + i).Append(';')
                  .Append("Stressartikel;")
                  .Append($"WZ-{i % 100:000}").Append(';')
                  .Append("8;1000;29.08.2026 22:00;Released;100;01")
                  .AppendLine();
            }
            await File.WriteAllTextAsync(source, sb.ToString(), new UTF8Encoding(false), cancellationToken);

            var proSettings = new ProAlphaConnectionSettings
            {
                SourceMode = ProAlphaSourceMode.FileExport,
                FilePath = source,
                CsvDelimiter = ";",
                HeaderRow = 1,
                CultureName = "de-DE"
            };
            var proMappings = new[]
            {
                new ProAlphaFieldMapping("MachineNumber", "MachineNumber", false, ""),
                new ProAlphaFieldMapping("MachineName", "MachineName", false, ""),
                new ProAlphaFieldMapping("MachineExternalId", "MachineId", false, ""),
                new ProAlphaFieldMapping("WorkCenter", "WorkCenter", false, ""),
                new ProAlphaFieldMapping("OrderNumber", "OrderNumber", true, ""),
                new ProAlphaFieldMapping("OperationNumber", "OperationNumber", false, ""),
                new ProAlphaFieldMapping("ArticleNumber", "ArticleNumber", true, ""),
                new ProAlphaFieldMapping("OrderQuantity", "OrderQuantity", true, ""),
                new ProAlphaFieldMapping("ArticleDescription", "ArticleDescription", false, ""),
                new ProAlphaFieldMapping("ToolNumber", "ToolNumber", false, ""),
                new ProAlphaFieldMapping("Cavities", "Cavities", false, ""),
                new ProAlphaFieldMapping("PackagingQuantity", "PackagingQuantity", false, ""),
                new ProAlphaFieldMapping("PlannedStart", "PlannedStart", false, ""),
                new ProAlphaFieldMapping("OrderStatus", "OrderStatus", false, ""),
                new ProAlphaFieldMapping("CompanyCode", "Company", false, ""),
                new ProAlphaFieldMapping("PlantCode", "Plant", false, "")
            };
            var proService = new ProAlphaIntegrationService();
            for (var pass = 1; pass <= 3; pass++)
            {
                var orders = await proService.LoadOrdersAsync(proSettings, proMappings, cancellationToken);
                if (orders.Count != orderCount)
                    errors.Add($"proALPHA Parser Pass {pass}: erwartet {orderCount:N0}, erhalten {orders.Count:N0}.");
            }
            notes.Add($"proALPHA Parserlast: {orderCount:N0} Datensätze × 3 Durchläufe");

            var alsSettings = new AlsConnectionSettings
            {
                SourceMode = AlsSourceMode.FileExport,
                FilePath = source,
                CsvDelimiter = ";",
                HeaderRow = 1,
                CultureName = "de-DE"
            };
            var alsMappings = new[]
            {
                new AlsFieldMapping("MachineNumber", "MachineNumber", false, ""),
                new AlsFieldMapping("MachineName", "MachineName", false, ""),
                new AlsFieldMapping("MachineExternalId", "MachineId", false, ""),
                new AlsFieldMapping("OrderNumber", "OrderNumber", true, ""),
                new AlsFieldMapping("OperationNumber", "OperationNumber", false, ""),
                new AlsFieldMapping("ArticleNumber", "ArticleNumber", true, ""),
                new AlsFieldMapping("OrderQuantity", "OrderQuantity", true, ""),
                new AlsFieldMapping("ArticleDescription", "ArticleDescription", false, ""),
                new AlsFieldMapping("ToolNumber", "ToolNumber", false, ""),
                new AlsFieldMapping("Cavities", "Cavities", false, ""),
                new AlsFieldMapping("PackagingQuantity", "PackagingQuantity", false, ""),
                new AlsFieldMapping("PlannedStart", "PlannedStart", false, ""),
                new AlsFieldMapping("OrderStatus", "OrderStatus", false, "")
            };
            var alsOrders = await new AlsIntegrationService().LoadOrdersAsync(alsSettings, alsMappings, cancellationToken);
            if (alsOrders.Count != orderCount)
                errors.Add($"ALS Parser: erwartet {orderCount:N0}, erhalten {alsOrders.Count:N0}.");
            notes.Add($"ALS Parserlast: {orderCount:N0} Datensätze");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static async Task<long> CountStressRowsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Default Timeout=15");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PackagingUnits WHERE OrderNumber LIKE 'STRESS-%';";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
                return;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("Partcounter/HF5-Initialisierung oder Zustandswechsel hat das Zeitlimit überschritten.");
    }

    private static async Task WriteReportAsync(
        string reportPath,
        bool success,
        TimeSpan elapsed,
        IReadOnlyList<string> notes,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        reportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var sb = new StringBuilder();
        sb.AppendLine("PARTCOUNTER STRESSTEST + OPERATING MODE ISOLATION");
        sb.AppendLine($"Revision: {AppVersionInfo.RevisionLabel}");
        sb.AppendLine($"Version: {AppVersionInfo.VersionText}");
        sb.AppendLine($"Build: {AppVersionInfo.InformationalVersion}");
        sb.AppendLine($"Zeit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Dauer: {elapsed}");
        sb.AppendLine($"Ergebnis: {(success ? "PASS" : "FAIL")}");
        sb.AppendLine("Vertrag: Simulation und Echtbetrieb getrennte MachineState-Instanzen; Simulation ohne Produktionspersistenz/Auto-Druck");
        sb.AppendLine();
        sb.AppendLine("MESSWERTE / NOTIZEN");
        foreach (var note in notes) sb.AppendLine($"- {note}");
        sb.AppendLine();
        sb.AppendLine("FEHLER");
        if (errors.Count == 0) sb.AppendLine("- keine");
        else foreach (var error in errors) sb.AppendLine($"- {error}");
        await File.WriteAllTextAsync(reportPath, sb.ToString(), new UTF8Encoding(false), cancellationToken);
    }
}
