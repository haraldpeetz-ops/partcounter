using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LabelPrintSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseService _database = new();
    private string ConnectionString => $"Data Source={_database.DatabasePath};Cache=Shared";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS LabelPrintSnapshots (
                PackagingUnitId TEXT PRIMARY KEY,
                TemplateId TEXT NOT NULL,
                TemplateName TEXT NOT NULL,
                TemplateUpdatedAtUtc TEXT NOT NULL,
                DefinitionJson TEXT NOT NULL,
                DefinitionSha256 TEXT NOT NULL,
                CapturedAtUtc TEXT NOT NULL,
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_LabelPrintSnapshots_CapturedAtUtc
                ON LabelPrintSnapshots(CapturedAtUtc DESC);
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<LabelPrintSnapshot> SaveSnapshotIfMissingAsync(
        PackagingUnitRecord record,
        LabelTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(template);
        await InitializeAsync();

        var existing = await LoadSnapshotAsync(record.Id);
        if (existing is not null)
            return existing;

        var json = JsonSerializer.Serialize(template, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var capturedAtUtc = DateTime.UtcNow;

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO LabelPrintSnapshots
                (PackagingUnitId, TemplateId, TemplateName, TemplateUpdatedAtUtc, DefinitionJson, DefinitionSha256, CapturedAtUtc)
            VALUES
                ($ve, $templateId, $templateName, $updated, $json, $hash, $captured);
            """;
        command.Parameters.AddWithValue("$ve", record.Id);
        command.Parameters.AddWithValue("$templateId", template.Id ?? string.Empty);
        command.Parameters.AddWithValue("$templateName", template.Name ?? string.Empty);
        command.Parameters.AddWithValue("$updated", template.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$captured", capturedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();

        return await LoadSnapshotAsync(record.Id)
            ?? throw new InvalidOperationException("Etiketten-Layout-Snapshot konnte nach dem Speichern nicht geladen werden.");
    }

    public async Task<LabelPrintSnapshot?> LoadSnapshotAsync(string packagingUnitId)
    {
        if (string.IsNullOrWhiteSpace(packagingUnitId))
            return null;

        await InitializeAsync();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PackagingUnitId, TemplateId, TemplateName, TemplateUpdatedAtUtc,
                   DefinitionJson, DefinitionSha256, CapturedAtUtc
            FROM LabelPrintSnapshots
            WHERE PackagingUnitId=$ve;
            """;
        command.Parameters.AddWithValue("$ve", packagingUnitId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var json = reader.GetString(4);
        var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var storedHash = reader.GetString(5);
        if (!string.Equals(actualHash, storedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Etiketten-Snapshot für VE {packagingUnitId} ist beschädigt (SHA-256-Prüfung fehlgeschlagen).");

        var template = JsonSerializer.Deserialize<LabelTemplateDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Etiketten-Snapshot für VE {packagingUnitId} enthält keine gültige Vorlage.");

        return new LabelPrintSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
            storedHash,
            DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            template);
    }
}
