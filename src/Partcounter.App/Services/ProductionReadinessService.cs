using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Partcounter.Services;

public sealed record DatabaseHealthResult(
    bool IsOk,
    string QuickCheck,
    int ForeignKeyViolations,
    string Summary);

public sealed class ProductionReadinessService
{
    public const int MaxBackupFiles = 30;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProductionReadinessService()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Partcounter");
        BackupDirectory = Path.Combine(DataDirectory, "Backups");
        DiagnosticDirectory = Path.Combine(DataDirectory, "Diagnostics");
        DatabasePath = Path.Combine(DataDirectory, "partcounter.db");
        StartupLogPath = Path.Combine(DataDirectory, "Partcounter_startup.log");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(DiagnosticDirectory);
    }

    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public string DiagnosticDirectory { get; }
    public string DatabasePath { get; }
    public string StartupLogPath { get; }

    public async Task<DatabaseHealthResult> CheckDatabaseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(CheckDatabaseCore);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> CreateBackupAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(CreateBackupCore);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> EnsureDailyBackupAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                Directory.CreateDirectory(BackupDirectory);
                var today = DateTime.Now.Date;
                var existingToday = Directory
                    .EnumerateFiles(BackupDirectory, "partcounter_*.db", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .Where(info => info.Exists)
                    .OrderByDescending(info => info.CreationTimeUtc)
                    .FirstOrDefault(info => info.CreationTime.Date == today);

                if (existingToday is not null)
                {
                    PruneBackupsCore();
                    return null;
                }

                return CreateBackupCore();
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> CreateDiagnosticPackageAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(CreateDiagnosticPackageCore);
        }
        finally
        {
            _gate.Release();
        }
    }

    public FileInfo? GetLatestBackup()
    {
        Directory.CreateDirectory(BackupDirectory);
        return Directory
            .EnumerateFiles(BackupDirectory, "partcounter_*.db", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .OrderByDescending(info => info.CreationTimeUtc)
            .FirstOrDefault();
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private DatabaseHealthResult CheckDatabaseCore()
    {
        if (!File.Exists(DatabasePath))
            return new DatabaseHealthResult(false, "Datenbankdatei fehlt", 0, $"Datenbank nicht gefunden: {DatabasePath}");

        using var connection = OpenReadOnlyConnection(DatabasePath);

        using var quick = connection.CreateCommand();
        quick.CommandText = "PRAGMA quick_check;";
        var quickResult = quick.ExecuteScalar()?.ToString() ?? "Keine Rückmeldung";

        var foreignKeyViolations = 0;
        using (var foreign = connection.CreateCommand())
        {
            foreign.CommandText = "PRAGMA foreign_key_check;";
            using var reader = foreign.ExecuteReader();
            while (reader.Read())
                foreignKeyViolations++;
        }

        var ok = string.Equals(quickResult, "ok", StringComparison.OrdinalIgnoreCase) && foreignKeyViolations == 0;
        var summary = ok
            ? "SQLite-Integritätsprüfung: OK · keine Fremdschlüsselverletzungen."
            : $"SQLite-Prüfung auffällig: quick_check={quickResult}; Fremdschlüsselverletzungen={foreignKeyViolations}.";

        return new DatabaseHealthResult(ok, quickResult, foreignKeyViolations, summary);
    }

    private string CreateBackupCore()
    {
        if (!File.Exists(DatabasePath))
            throw new FileNotFoundException("Die Partcounter-Datenbank wurde nicht gefunden.", DatabasePath);

        Directory.CreateDirectory(BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(BackupDirectory, $"partcounter_{stamp}.db");
        if (File.Exists(backupPath))
            backupPath = Path.Combine(BackupDirectory, $"partcounter_{stamp}_{Guid.NewGuid():N}"[..47] + ".db");

        using (var source = OpenReadOnlyConnection(DatabasePath))
        using (var target = OpenReadWriteCreateConnection(backupPath))
        {
            source.BackupDatabase(target);
        }

        using (var verify = OpenReadOnlyConnection(backupPath))
        using (var command = verify.CreateCommand())
        {
            command.CommandText = "PRAGMA quick_check;";
            var result = command.ExecuteScalar()?.ToString() ?? string.Empty;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(backupPath);
                throw new InvalidDataException($"Die erstellte Sicherung hat die SQLite-Prüfung nicht bestanden: {result}");
            }
        }

        PruneBackupsCore();
        return backupPath;
    }

    private string CreateDiagnosticPackageCore()
    {
        Directory.CreateDirectory(DiagnosticDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var packagePath = Path.Combine(DiagnosticDirectory, $"Partcounter_Diagnose_R001_15_{stamp}.zip");

        var health = CheckDatabaseCore();
        var events = ReadRecentEventsCore();
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "–";
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        AddTextEntry(archive, "manifest.txt", BuildManifest(version, informational));
        AddTextEntry(archive, "database-health.txt", BuildDatabaseHealthText(health));
        AddTextEntry(archive, "recent-events.tsv", events);

        if (File.Exists(StartupLogPath))
        {
            try
            {
                archive.CreateEntryFromFile(StartupLogPath, "Partcounter_startup.log", CompressionLevel.Optimal);
            }
            catch
            {
                AddTextEntry(archive, "startup-log-note.txt", "Das Startprotokoll konnte beim Erstellen des Diagnosepakets nicht gelesen werden.");
            }
        }

        return packagePath;
    }

    private string ReadRecentEventsCore()
    {
        if (!File.Exists(DatabasePath))
            return "CreatedAtUtc\tMachine\tCategory\tMessage\r\nDatenbank nicht gefunden.\r\n";

        var sb = new StringBuilder();
        sb.AppendLine("CreatedAtUtc\tMachine\tCategory\tMessage");

        using var connection = OpenReadOnlyConnection(DatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CreatedAtUtc, MachineNumber, Category, Message
            FROM Events
            ORDER BY Id DESC
            LIMIT 250;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var created = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var machine = reader.IsDBNull(1) ? string.Empty : reader.GetInt32(1).ToString();
            var category = reader.IsDBNull(2) ? string.Empty : SanitizeTsv(reader.GetString(2));
            var message = reader.IsDBNull(3) ? string.Empty : SanitizeTsv(reader.GetString(3));
            sb.Append(created).Append('\t')
              .Append(machine).Append('\t')
              .Append(category).Append('\t')
              .Append(message).AppendLine();
        }

        return sb.ToString();
    }

    private string BuildManifest(string version, string informational)
    {
        var latestBackup = GetLatestBackup();
        var sb = new StringBuilder();
        sb.AppendLine("PARTCOUNTER DIAGNOSEPAKET");
        sb.AppendLine("Revision: R001.15");
        sb.AppendLine($"Assembly: {version}");
        sb.AppendLine($"Build: {informational}");
        sb.AppendLine($"Erstellt lokal: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Erstellt UTC: {DateTime.UtcNow:O}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Prozessarchitektur: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"App-Verzeichnis: {AppContext.BaseDirectory}");
        sb.AppendLine($"Datenverzeichnis: {DataDirectory}");
        sb.AppendLine($"Datenbank: {DatabasePath}");
        sb.AppendLine($"Letzte Sicherung: {(latestBackup is null ? "keine" : latestBackup.FullName)}");
        sb.AppendLine();
        sb.AppendLine("Datenschutz / Sicherheit:");
        sb.AppendLine("Dieses Diagnosepaket enthält KEINE Settings-Tabelle und KEINE Datenbanksicherung.");
        sb.AppendLine("Enthalten sind nur Systeminformationen, SQLite-Prüfergebnis, Startprotokoll und die letzten Ereigniseinträge.");
        return sb.ToString();
    }

    private static string BuildDatabaseHealthText(DatabaseHealthResult health) =>
        $"SQLite quick_check: {health.QuickCheck}{Environment.NewLine}" +
        $"Fremdschlüsselverletzungen: {health.ForeignKeyViolations}{Environment.NewLine}" +
        $"Status: {(health.IsOk ? "OK" : "FEHLER")}{Environment.NewLine}" +
        $"Bewertung: {health.Summary}{Environment.NewLine}";

    private void PruneBackupsCore()
    {
        var obsolete = Directory
            .EnumerateFiles(BackupDirectory, "partcounter_*.db", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .OrderByDescending(info => info.CreationTimeUtc)
            .Skip(MaxBackupFiles)
            .ToList();

        foreach (var file in obsolete)
            TryDelete(file.FullName);
    }

    private static SqliteConnection OpenReadOnlyConnection(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static SqliteConnection OpenReadWriteCreateConnection(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string SanitizeTsv(string value) =>
        value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Aufräumfehler dürfen eine erfolgreiche Sicherung nicht entwerten.
        }
    }
}
