using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LabelReprintService
{
    private readonly DatabaseService _database = new();
    private readonly LabelPrintService _printer = new();

    private string ConnectionString => $"Data Source={_database.DatabasePath};Cache=Shared";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
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
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                ON LabelReprintJournal(PackagingUnitId, Id DESC);
            """;
        await command.ExecuteNonQueryAsync();
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

        bool successful;
        string errorMessage;

        if (string.IsNullOrWhiteSpace(normalizedPrinter))
        {
            successful = false;
            errorMessage = "Kein Windows-Druckername konfiguriert.";
        }
        else
        {
            successful = await _printer.PrintAsync(record, normalizedPrinter);
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
            errorMessage));

        var eventMessage = successful
            ? $"Nachdruck #{reprintNumber} für VE-ID {record.Id}; VE {record.VeNumber}; Auftrag {record.OrderNumber}; Artikel {record.ArticleNumber}; Drucker {normalizedPrinter}; Grund: {normalizedReason}."
            : $"Nachdruck #{reprintNumber} FEHLER für VE-ID {record.Id}; Drucker {normalizedPrinter}; Grund: {normalizedReason}; Fehler: {errorMessage}";

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
            attemptedAtUtc);
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
            SELECT Id, PackagingUnitId, ReprintNumber, PrintedAtUtc, PrinterName, Reason, Successful, ErrorMessage
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
                reader.GetString(7)));
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
                (PackagingUnitId, ReprintNumber, PrintedAtUtc, PrinterName, Reason, Successful, ErrorMessage)
            VALUES
                ($id, $number, $time, $printer, $reason, $successful, $error);
            """;
        command.Parameters.AddWithValue("$id", entry.PackagingUnitId);
        command.Parameters.AddWithValue("$number", entry.ReprintNumber);
        command.Parameters.AddWithValue("$time", entry.PrintedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$printer", entry.PrinterName);
        command.Parameters.AddWithValue("$reason", entry.Reason);
        command.Parameters.AddWithValue("$successful", entry.Successful ? 1 : 0);
        command.Parameters.AddWithValue("$error", entry.ErrorMessage ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }
}
