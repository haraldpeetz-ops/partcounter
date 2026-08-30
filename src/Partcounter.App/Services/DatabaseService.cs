using System.IO;
using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class DatabaseService
{
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);

    public DatabaseService()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Partcounter");
        Directory.CreateDirectory(baseDirectory);
        DatabasePath = Path.Combine(baseDirectory, "partcounter.db");
    }

    public string DatabasePath { get; }

    // SQLite supports many readers but only one writer at a time. The previous implementation allowed
    // many VE-completion callbacks to compete for the write lock. R001.23 deliberately serializes the
    // application's DatabaseService writes and gives SQLite a bounded busy timeout.
    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(DatabasePath);

    public async Task InitializeAsync()
    {
        await InitializationGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=15000;

                CREATE TABLE IF NOT EXISTS Machines (
                    MachineNumber INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    Port INTEGER NOT NULL DEFAULT 502,
                    UnitId INTEGER NOT NULL DEFAULT 1,
                    Enabled INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Articles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArticleNumber TEXT NOT NULL UNIQUE,
                    Description TEXT NOT NULL,
                    ToolNumber TEXT NOT NULL,
                    ActiveCavities INTEGER NOT NULL CHECK(ActiveCavities BETWEEN 1 AND 64),
                    PackagingQuantity INTEGER NOT NULL CHECK(PackagingQuantity > 0),
                    Active INTEGER NOT NULL DEFAULT 1,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PackagingUnits (
                    Id TEXT PRIMARY KEY,
                    MachineNumber INTEGER NOT NULL,
                    MachineName TEXT NOT NULL,
                    VeNumber INTEGER NOT NULL,
                    OrderNumber TEXT NOT NULL,
                    ArticleNumber TEXT NOT NULL,
                    ArticleDescription TEXT NOT NULL,
                    ToolNumber TEXT NOT NULL,
                    Cavities INTEGER NOT NULL,
                    TargetQuantity INTEGER NOT NULL,
                    ActualQuantity INTEGER NOT NULL,
                    Overfill INTEGER NOT NULL,
                    CompletionReason INTEGER NOT NULL,
                    CompletedAtUtc TEXT NOT NULL,
                    LabelStatus TEXT NOT NULL,
                    PrintedAtUtc TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PackagingUnits_CompletedAtUtc
                    ON PackagingUnits(CompletedAtUtc DESC);

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Events (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CreatedAtUtc TEXT NOT NULL,
                    MachineNumber INTEGER NULL,
                    Category TEXT NOT NULL,
                    Message TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();

            await SeedMachinesAsync(connection);
            await SeedArticlesAsync(connection);
            await EnsureDefaultSettingsAsync(connection);
        }
        finally
        {
            InitializationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MachineConfiguration>> LoadMachinesAsync()
    {
        var result = new List<MachineConfiguration>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT MachineNumber, Name, IpAddress, Port, UnitId, Enabled FROM Machines ORDER BY MachineNumber;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new MachineConfiguration(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                checked((byte)reader.GetInt32(4)),
                reader.GetInt32(5) != 0));
        }

        return result;
    }

    public async Task<IReadOnlyList<ArticleDefinition>> LoadArticlesAsync()
    {
        var result = new List<ArticleDefinition>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ArticleNumber, Description, ToolNumber, ActiveCavities, PackagingQuantity, Active
            FROM Articles
            ORDER BY ArticleNumber COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ArticleDefinition(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                checked((ushort)reader.GetInt32(4)),
                checked((uint)reader.GetInt64(5)),
                reader.GetInt32(6) != 0));
        }

        return result;
    }

    public async Task UpsertArticleAsync(ArticleDefinition article)
    {
        if (string.IsNullOrWhiteSpace(article.ArticleNumber))
            throw new ArgumentException("Artikelnummer darf nicht leer sein.");
        if (article.ActiveCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(article.ActiveCavities), "Kavitäten müssen zwischen 1 und 64 liegen.");
        if (article.PackagingQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(article.PackagingQuantity), "VE-Menge muss größer 0 sein.");

        await ExecuteWriteAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Articles
                    (ArticleNumber, Description, ToolNumber, ActiveCavities, PackagingQuantity, Active, UpdatedAtUtc)
                VALUES
                    ($article, $description, $tool, $cavities, $quantity, $active, $updated)
                ON CONFLICT(ArticleNumber) DO UPDATE SET
                    Description = excluded.Description,
                    ToolNumber = excluded.ToolNumber,
                    ActiveCavities = excluded.ActiveCavities,
                    PackagingQuantity = excluded.PackagingQuantity,
                    Active = excluded.Active,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;
                """;
            command.Parameters.AddWithValue("$article", article.ArticleNumber.Trim());
            command.Parameters.AddWithValue("$description", article.Description.Trim());
            command.Parameters.AddWithValue("$tool", article.ToolNumber.Trim());
            command.Parameters.AddWithValue("$cavities", (int)article.ActiveCavities);
            command.Parameters.AddWithValue("$quantity", (long)article.PackagingQuantity);
            command.Parameters.AddWithValue("$active", article.Active ? 1 : 0);
            command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public Task SavePackagingUnitAsync(PackagingUnitRecord record) => ExecuteWriteAsync(async connection =>
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO PackagingUnits
                (Id, MachineNumber, MachineName, VeNumber, OrderNumber, ArticleNumber, ArticleDescription,
                 ToolNumber, Cavities, TargetQuantity, ActualQuantity, Overfill, CompletionReason,
                 CompletedAtUtc, LabelStatus, PrintedAtUtc)
            VALUES
                ($id, $machineNumber, $machineName, $veNumber, $orderNumber, $articleNumber, $articleDescription,
                 $toolNumber, $cavities, $targetQuantity, $actualQuantity, $overfill, $completionReason,
                 $completedAtUtc, $labelStatus, $printedAtUtc);
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$machineNumber", record.MachineNumber);
        command.Parameters.AddWithValue("$machineName", record.MachineName);
        command.Parameters.AddWithValue("$veNumber", (int)record.VeNumber);
        command.Parameters.AddWithValue("$orderNumber", record.OrderNumber);
        command.Parameters.AddWithValue("$articleNumber", record.ArticleNumber);
        command.Parameters.AddWithValue("$articleDescription", record.ArticleDescription);
        command.Parameters.AddWithValue("$toolNumber", record.ToolNumber);
        command.Parameters.AddWithValue("$cavities", (int)record.Cavities);
        command.Parameters.AddWithValue("$targetQuantity", (long)record.TargetQuantity);
        command.Parameters.AddWithValue("$actualQuantity", (long)record.ActualQuantity);
        command.Parameters.AddWithValue("$overfill", (long)record.Overfill);
        command.Parameters.AddWithValue("$completionReason", (int)record.CompletionReason);
        command.Parameters.AddWithValue("$completedAtUtc", record.CompletedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$labelStatus", record.LabelStatus);
        command.Parameters.AddWithValue("$printedAtUtc", record.PrintedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    });

    public Task UpdateLabelStatusAsync(string id, string labelStatus, DateTime? printedAtUtc) => ExecuteWriteAsync(async connection =>
    {
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE PackagingUnits SET LabelStatus=$status, PrintedAtUtc=$printed WHERE Id=$id;";
        command.Parameters.AddWithValue("$status", labelStatus);
        command.Parameters.AddWithValue("$printed", printedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    });

    public async Task<IReadOnlyList<PackagingUnitRecord>> LoadRecentPackagingUnitsAsync(int limit = 100)
    {
        var result = new List<PackagingUnitRecord>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, MachineNumber, MachineName, VeNumber, OrderNumber, ArticleNumber, ArticleDescription,
                   ToolNumber, Cavities, TargetQuantity, ActualQuantity, Overfill, CompletionReason,
                   CompletedAtUtc, LabelStatus, PrintedAtUtc
            FROM PackagingUnits
            ORDER BY CompletedAtUtc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var printed = reader.IsDBNull(15) ? (DateTime?)null : DateTime.Parse(reader.GetString(15), null, System.Globalization.DateTimeStyles.RoundtripKind);
            result.Add(new PackagingUnitRecord(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                checked((ushort)reader.GetInt32(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                checked((ushort)reader.GetInt32(8)),
                checked((uint)reader.GetInt64(9)),
                checked((uint)reader.GetInt64(10)),
                checked((uint)reader.GetInt64(11)),
                (VeCompletionReason)reader.GetInt32(12),
                DateTime.Parse(reader.GetString(13), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(14),
                printed));
        }

        return result;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key=$key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync() as string;
    }

    public Task SetSettingAsync(string key, string value) => ExecuteWriteAsync(async connection =>
    {
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Settings(Key, Value) VALUES($key, $value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    });

    public Task AddEventAsync(int? machineNumber, string category, string message) => ExecuteWriteAsync(async connection =>
    {
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Events(CreatedAtUtc, MachineNumber, Category, Message) VALUES($time, $machine, $category, $message);";
        command.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$machine", machineNumber.HasValue ? machineNumber.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$message", message);
        await command.ExecuteNonQueryAsync();
    });

    private Task ExecuteWriteAsync(Func<SqliteConnection, Task> write) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write);

    public Task ExecuteExclusiveWriteAsync(Func<SqliteConnection, Task> write, CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write, cancellationToken);

    public Task<T> ExecuteExclusiveWriteAsync<T>(Func<SqliteConnection, Task<T>> write, CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write, cancellationToken);

    private static async Task SeedMachinesAsync(SqliteConnection connection)
    {
        for (var i = 1; i <= 30; i++)
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO Machines(MachineNumber, Name, IpAddress, Port, UnitId, Enabled)
                VALUES($number, $name, $ip, 502, 1, 1);
                """;
            command.Parameters.AddWithValue("$number", i);
            command.Parameters.AddWithValue("$name", $"Spritzgussmaschine {i:00}");
            command.Parameters.AddWithValue("$ip", $"192.168.50.{100 + i}");
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedArticlesAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Articles(ArticleNumber, Description, ToolNumber, ActiveCavities, PackagingQuantity, Active, UpdatedAtUtc)
            VALUES
                ('DEMO-1000', 'Demoartikel 8-fach / VE 1000', 'WZ-DEMO-08', 8, 1000, 1, $updated),
                ('DEMO-064', 'Demo Aufrundung 64-fach / VE 1000', 'WZ-DEMO-64', 64, 1000, 1, $updated);
            """;
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureDefaultSettingsAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Settings(Key, Value) VALUES('AutoPrintLabels', 'true');
            INSERT OR IGNORE INTO Settings(Key, Value) VALUES('LabelPrinterName', '');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
