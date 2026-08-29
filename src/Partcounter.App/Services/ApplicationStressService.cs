using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Partcounter.Models;
using Partcounter.ViewModels;

namespace Partcounter.Services;

/// <summary>
/// Headless/CI stress run against the real WPF application in simulation mode.
/// It deliberately exercises the normal MainViewModel/MachineState/SQLite event path,
/// but never enables Modbus real operation and never sends PLC writes.
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
                () => window.DataContext is MainViewModel vm && vm.Machines.Count == 30 && vm.Articles.Count > 0,
                TimeSpan.FromSeconds(45),
                cancellationToken);

            if (window.DataContext is not MainViewModel vm)
                throw new InvalidOperationException("MainViewModel wurde im Stresslauf nicht gefunden.");
            if (!vm.IsSimulationMode)
                throw new InvalidOperationException("Stresslauf darf ausschließlich im Simulationsmodus starten.");

            vm.AutoPrintLabels = false;
            var article = vm.Articles.FirstOrDefault(a => a.ActiveCavities == 64)
                ?? vm.Articles.First();

            foreach (var machine in vm.Machines)
                machine.VeCompleted += CountVe;

            notes.Add($"Maschinen: {vm.Machines.Count}");
            notes.Add($"Stressartikel: {article.ArticleNumber}, Kavitäten={article.ActiveCavities}, VE={article.PackagingQuantity}");

            const int rounds = 4;
            const uint targetPerRound = 64_000;
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
                var maxCycles = (int)Math.Ceiling(targetPerRound / (double)cavityCount) + 64;
                for (var cycle = 0; cycle < maxCycles; cycle++)
                {
                    foreach (var machine in vm.Machines)
                    {
                        if (machine.OrderState == ProductionOrderState.Running)
                            machine.ApplySimulationCycle();
                    }

                    if (cycle % 40 == 0)
                    {
                        process.Refresh();
                        peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                        peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        await Task.Delay(1, cancellationToken);
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
            var databasePath = new DatabaseService().DatabasePath;
            var persisted = await WaitForStressRowsAsync(databasePath, expected, TimeSpan.FromSeconds(90), cancellationToken);
            if (persisted < expected)
                errors.Add($"SQLite: nur {persisted:N0} von {expected:N0} ausgelösten Stress-VE-Datensätzen persistiert.");

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

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.PeakWorkingSet64);
            peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));

            notes.Add($"VE-Ereignisse gesamt: {expected:N0}");
            notes.Add($"Persistierte Stress-VE: {persisted:N0}");
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

    private static async Task<long> WaitForStressRowsAsync(
        string databasePath,
        long expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + timeout;
        long count = 0;
        while (DateTime.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Default Timeout=15");
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM PackagingUnits WHERE OrderNumber LIKE 'STRESS-%';";
            count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            if (count >= expected)
                return count;
            await Task.Delay(250, cancellationToken);
        }
        return count;
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
        throw new TimeoutException("Partcounter-Initialisierung hat im Stresslauf das Zeitlimit überschritten.");
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
        sb.AppendLine("PARTCOUNTER STRESSTEST");
        sb.AppendLine($"Revision: {AppVersionInfo.Revision}");
        sb.AppendLine($"Version: {AppVersionInfo.VersionText}");
        sb.AppendLine($"Build: {AppVersionInfo.InformationalVersion}");
        sb.AppendLine($"Zeit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Dauer: {elapsed}");
        sb.AppendLine($"Ergebnis: {(success ? "PASS" : "FAIL")}");
        sb.AppendLine("Modus: SIMULATION ONLY - keine Modbus-Schreibbefehle");
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
