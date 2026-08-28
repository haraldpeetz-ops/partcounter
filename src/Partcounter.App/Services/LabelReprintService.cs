using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LabelReprintService
{
    private readonly DatabaseService _database = new();
    private readonly LabelPrintService _printer = new();
    private readonly LabelTemplateService _templates = new();
    private readonly LabelPrintSnapshotService _snapshots = new();

    private string ConnectionString => $"Data Source={_database.DatabasePath};Cache=Shared";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _snapshots.InitializeAsync();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS LabelReprintJournal (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PackagingUnitId TEXT NOT NULL,
                ReprintNumber INTEGER NOT NULL,
                PrintedAtUtc TEXT NOT NULL,
                PrinterName TEXT NOT NULL,
                Reason TEXT NOT NULL,
                Successful INTEGER NOT NULL,
                ErrorMessage TEXT NOT NULL DEFAULT '',
                LayoutSource TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                ON LabelReprintJournal(PackagingUnitId, Id DESC);
            """;
        await command.ExecuteNonQueryAsync();
        await EnsureColumnAsync(connection, "LabelReprintJournal", "LayoutSource", "TEXT NOT NULL DEFAULT ''");
    }

    public async Task<LabelReprintResult> ReprintAsync(
        PackagingUnitRecord record,
        string printerName,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(record);
        await InitializeAsync();

        var normalizedPrinter = (printerName ?? string.Empty).Trim();
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Nicht angegeben" : reason.Trim();
        var reprintNumber = await GetNextReprintNumberAsync(record.Id);
        var attemptedAtUtc = DateTime.UtcNow;

        var snapshot = await _snapshots.LoadSnapshotAsync(record.Id);
        LabelTemplateDefinition template;
        string layoutSource;
        var historicalSnapshotUsed = snapshot is not null;

        if (snapshot is not null)
        {
            template = snapshot.Template;
            layoutSource = $"Original-Snapshot: {snapshot.TemplateName} · {snapshot.TemplateId} · SHA256 {snapshot.ShortHash}…";
        }
        else
        {
            template = await _templates.ResolveTemplateAsync(record);
            layoutSource = $"Fallback aktuelles Layout: {template.Name} · {template.Id} · kein historischer Snapshot verfügbar";
        }

        bool successful;
        string errorMessage;

        if (string.IsNullOrWhiteSpace(normalizedPrinter))
        {
            successful = false;
            errorMessage = "Kein Windows-Druckername konfiguriert.";
        }
        else
        {
            successful = await _printer.PrintTemplateAsync(record, template, normalizedPrinter);
            errorMessage = successful
                ? string.Empty
                : "Der Druckauftrag konnte nicht an die Windows-Druckerwarteschlange übergeben werden. Druckername, Queue und Treiber prüfen.";
        }

        await AddJournalEntryAsync(new LabelReprintJournalEntry(
            0,
            record.Id,
            reprintNumber,
            attemptedAtUtc,
            normalizedPrinter,
            normalizedReason,
            successful,
            errorMessage,
            layoutSource));

        var eventMessage = successful
            ? $"Nachdruck #{reprintNumber} für VE-ID {record.Id}; VE {record.VeNumber}; Auftrag {record.OrderNumber}; Artikel {record.ArticleNumber}; Drucker {normalizedPrinter}; Grund: {normalizedReason}; Layout: {layoutSource}."
            : $"Nachdruck #{reprintNumber} FEHLER für VE-ID {record.Id}; Drucker {normalizedPrinter}; Grund: {normalizedReason}; Layout: {layoutSource}; Fehler: {errorMessage}";

        await _database.AddEventAsync(
            record.MachineNumber,
            successful ? "LABEL_REPRINT_OK" : "LABEL_REPRINT_ERROR",
            eventMessage);

        return new LabelReprintResult(
            successful,
            reprintNumber,
            normalizedPrinter,
            normalizedReason,
            errorMessage,
            attemptedAtUtc,
            layoutSource,
            historicalSnapshotUsed);
    }

    public async Task<IReadOnlyList<LabelReprintJournalEntry>> LoadJournalAsync(
        string packagingUnitId,
        int limit = 50)
    {
        await InitializeAsync();
        var result = new List<LabelReprintJournalEntry>();

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, PackagingUnitId, ReprintNumber, PrintedAtUtc, PrinterName, Reason,
                   Successful, ErrorMessage, LayoutSource
            FROM LabelReprintJournal
            WHERE PackagingUnitId=$id
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$id", packagingUnitId);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new LabelReprintJournalEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6) != 0,
                reader.GetString(7),
                reader.IsDBNull(8) ? string.Empty : reader.GetString(8)));
        }

        return result;
    }

    public async Task<int> GetSuccessfulReprintCountAsync(string packagingUnitId)
    {
        await InitializeAsync();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LabelReprintJournal WHERE PackagingUnitId=$id AND Successful=1;";
        command.Parameters.AddWithValue("$id", packagingUnitId);
        return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
    }

    public Task<LabelPrintSnapshot?> LoadPrintSnapshotAsync(string packagingUnitId) =>
        _snapshots.LoadSnapshotAsync(packagingUnitId);

    private async Task<int> GetNextReprintNumberAsync(string packagingUnitId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(ReprintNumber), 0) + 1 FROM LabelReprintJournal WHERE PackagingUnitId=$id;";
        command.Parameters.AddWithValue("$id", packagingUnitId);
        return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 1);
    }

    private async Task AddJournalEntryAsync(LabelReprintJournalEntry entry)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LabelReprintJournal
                (PackagingUnitId, ReprintNumber, PrintedAtUtc, PrinterName, Reason, Successful, ErrorMessage, LayoutSource)
            VALUES
                ($id, $number, $time, $printer, $reason, $successful, $error, $layout);
            """;
        command.Parameters.AddWithValue("$id", entry.PackagingUnitId);
        command.Parameters.AddWithValue("$number", entry.ReprintNumber);
        command.Parameters.AddWithValue("$time", entry.PrintedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$printer", entry.PrinterName);
        command.Parameters.AddWithValue("$reason", entry.Reason);
        command.Parameters.AddWithValue("$successful", entry.Successful ? 1 : 0);
        command.Parameters.AddWithValue("$error", entry.ErrorMessage ?? string.Empty);
        command.Parameters.AddWithValue("$layout", entry.LayoutSource ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string definition)
    {
        var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await check.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        await reader.DisposeAsync();

        if (exists)
            return;

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        await alter.ExecuteNonQueryAsync();
    }
}
