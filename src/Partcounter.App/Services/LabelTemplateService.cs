using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LabelTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _databasePath;

    public LabelTemplateService()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Partcounter");
        Directory.CreateDirectory(baseDirectory);
        _databasePath = Path.Combine(baseDirectory, "partcounter.db");
    }

    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_databasePath);

    public static IReadOnlyList<LabelDataToken> AvailableTokens { get; } = new List<LabelDataToken>
    {
        new("{{VE_ID}}", "Eindeutige VE-ID", "VE-M01-0001-20260826..."),
        new("{{MachineNumber}}", "Maschinennummer zweistellig", "01"),
        new("{{MachineName}}", "Maschinenname", "Spritzgussmaschine 01"),
        new("{{VeNumber}}", "VE-Nummer vierstellig", "0001"),
        new("{{OrderNumber}}", "Auftragsnummer", "AUF-20260826-001"),
        new("{{ArticleNumber}}", "Artikelnummer", "4711-0815"),
        new("{{ArticleDescription}}", "Artikelbezeichnung", "Gehäuse schwarz"),
        new("{{ToolNumber}}", "Werkzeugnummer", "WZ-1042"),
        new("{{Cavities}}", "Aktive Kavitäten", "8"),
        new("{{TargetQuantity}}", "VE-Sollmenge", "1000"),
        new("{{ActualQuantity}}", "Tatsächliche VE-Menge", "1000"),
        new("{{Overfill}}", "Zyklusbedingte Mehrmenge", "0"),
        new("{{CompletionReason}}", "Abschlussart", "Automatisch"),
        new("{{CompletedDate}}", "Fertigstellungsdatum", "26.08.2026"),
        new("{{CompletedTime}}", "Fertigstellungszeit", "22:45:12"),
        new("{{CompletedAt}}", "Datum und Uhrzeit", "26.08.2026 22:45:12"),
        new("{{QrPayload}}", "Partcounter QR-Nutzlast", "PC1|VE=...|M=01|A=..."),
    };

    public async Task InitializeAsync()
    {
        var needsDefault = await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelTemplates (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    WidthMm REAL NOT NULL,
                    HeightMm REAL NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    AssignedArticleNumber TEXT NULL,
                    DefinitionJson TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_LabelTemplates_Article
                    ON LabelTemplates(AssignedArticleNumber);
                """;
            await command.ExecuteNonQueryAsync();
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM LabelTemplates;";
            return Convert.ToInt32(await countCommand.ExecuteScalarAsync()) == 0;
        });
        if (needsDefault)
            await SaveTemplateAsync(CreateLegacyCompatibleDefaultTemplate());
    }

    public async Task<IReadOnlyList<LabelTemplateDefinition>> LoadTemplatesAsync()
    {
        await InitializeAsync();
        var result = new List<LabelTemplateDefinition>();

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DefinitionJson, Name, WidthMm, HeightMm, IsDefault, AssignedArticleNumber, UpdatedAtUtc
            FROM LabelTemplates
            ORDER BY IsDefault DESC, Name COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            try
            {
                var template = JsonSerializer.Deserialize<LabelTemplateDefinition>(reader.GetString(0), JsonOptions);
                if (template is null)
                    continue;

                // Die relationalen Spalten sind für Auswahl/Zuordnung maßgeblich. Dadurch bleiben
                // Default- und Artikelzuordnungen eindeutig, auch wenn ältere JSON-Snapshots noch
                // einen früheren Status enthalten.
                template.Name = reader.GetString(1);
                template.WidthMm = reader.GetDouble(2);
                template.HeightMm = reader.GetDouble(3);
                template.IsDefault = reader.GetInt32(4) != 0;
                template.AssignedArticleNumber = reader.IsDBNull(5) ? null : reader.GetString(5);
                template.UpdatedAtUtc = DateTime.Parse(
                    reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind);
                result.Add(template);
            }
            catch
            {
                // Defekte Einzelvorlage darf den Etikettendruck nicht komplett blockieren.
            }
        }

        if (result.Count == 0)
        {
            var fallback = CreateLegacyCompatibleDefaultTemplate();
            await SaveTemplateAsync(fallback);
            result.Add(fallback);
        }

        return result;
    }

    public async Task SaveTemplateAsync(LabelTemplateDefinition template)
    {
        ValidateTemplate(template);
        template.UpdatedAtUtc = DateTime.UtcNow;
        var assignedArticle = string.IsNullOrWhiteSpace(template.AssignedArticleNumber)
            ? null
            : template.AssignedArticleNumber.Trim();
        template.AssignedArticleNumber = assignedArticle;

        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync();

            if (template.IsDefault)
        {
            var clearDefault = connection.CreateCommand();
            clearDefault.Transaction = (SqliteTransaction)transaction;
            clearDefault.CommandText = "UPDATE LabelTemplates SET IsDefault=0 WHERE Id<>$id;";
            clearDefault.Parameters.AddWithValue("$id", template.Id);
            await clearDefault.ExecuteNonQueryAsync();
        }

        if (!string.IsNullOrWhiteSpace(assignedArticle))
        {
            var clearArticle = connection.CreateCommand();
            clearArticle.Transaction = (SqliteTransaction)transaction;
            clearArticle.CommandText = """
                UPDATE LabelTemplates
                SET AssignedArticleNumber=NULL
                WHERE Id<>$id AND lower(trim(AssignedArticleNumber))=lower(trim($article));
                """;
            clearArticle.Parameters.AddWithValue("$id", template.Id);
            clearArticle.Parameters.AddWithValue("$article", assignedArticle);
            await clearArticle.ExecuteNonQueryAsync();
        }

        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO LabelTemplates
                (Id, Name, WidthMm, HeightMm, IsDefault, AssignedArticleNumber, DefinitionJson, UpdatedAtUtc)
            VALUES
                ($id, $name, $width, $height, $default, $article, $json, $updated)
            ON CONFLICT(Id) DO UPDATE SET
                Name=excluded.Name,
                WidthMm=excluded.WidthMm,
                HeightMm=excluded.HeightMm,
                IsDefault=excluded.IsDefault,
                AssignedArticleNumber=excluded.AssignedArticleNumber,
                DefinitionJson=excluded.DefinitionJson,
                UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$id", template.Id);
        command.Parameters.AddWithValue("$name", template.Name.Trim());
        command.Parameters.AddWithValue("$width", template.WidthMm);
        command.Parameters.AddWithValue("$height", template.HeightMm);
        command.Parameters.AddWithValue("$default", template.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$article", assignedArticle is null ? DBNull.Value : assignedArticle);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(template, JsonOptions));
        command.Parameters.AddWithValue("$updated", template.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        });
    }

    public async Task DeleteTemplateAsync(string id)
    {
        await InitializeAsync();
        var templates = await LoadTemplatesAsync();
        if (templates.Count <= 1)
            throw new InvalidOperationException("Die letzte Etikettenvorlage kann nicht gelöscht werden.");

        var deleting = templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        if (deleting is null)
            return;

        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LabelTemplates WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        });

        if (deleting.IsDefault)
        {
            var replacement = (await LoadTemplatesAsync()).First();
            replacement.IsDefault = true;
            await SaveTemplateAsync(replacement);
        }
    }

    public async Task<LabelTemplateDefinition> ResolveTemplateAsync(PackagingUnitRecord record)
    {
        var templates = await LoadTemplatesAsync();
        return templates.FirstOrDefault(t =>
                   !string.IsNullOrWhiteSpace(t.AssignedArticleNumber) &&
                   string.Equals(t.AssignedArticleNumber.Trim(), record.ArticleNumber.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? templates.FirstOrDefault(t => t.IsDefault)
               ?? templates.First();
    }

    public static string ResolveContent(string? source, PackagingUnitRecord record)
    {
        var text = source ?? string.Empty;
        var local = record.CompletedAtUtc.ToLocalTime();
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VE_ID"] = record.Id,
            ["MachineNumber"] = record.MachineNumber.ToString("00"),
            ["MachineName"] = record.MachineName,
            ["VeNumber"] = record.VeNumber.ToString("0000"),
            ["OrderNumber"] = record.OrderNumber,
            ["ArticleNumber"] = record.ArticleNumber,
            ["ArticleDescription"] = record.ArticleDescription,
            ["ToolNumber"] = record.ToolNumber,
            ["Cavities"] = record.Cavities.ToString(),
            ["TargetQuantity"] = record.TargetQuantity.ToString("N0"),
            ["ActualQuantity"] = record.ActualQuantity.ToString("N0"),
            ["Overfill"] = record.Overfill.ToString("N0"),
            ["CompletionReason"] = record.CompletionReason switch
            {
                VeCompletionReason.AutomaticFull => "Automatisch",
                VeCompletionReason.Manual => "Manuell",
                _ => "Unbekannt"
            },
            ["CompletedDate"] = local.ToString("dd.MM.yyyy"),
            ["CompletedTime"] = local.ToString("HH:mm:ss"),
            ["CompletedAt"] = local.ToString("dd.MM.yyyy HH:mm:ss"),
            ["QrPayload"] = BuildQrPayload(record)
        };

        return Regex.Replace(text, @"\{\{(?<key>[A-Za-z0-9_]+)\}\}", match =>
        {
            var key = match.Groups["key"].Value;
            return replacements.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    public static LabelTemplateDefinition CreateLegacyCompatibleDefaultTemplate()
    {
        var t = new LabelTemplateDefinition
        {
            Id = "partcounter-standard-v1",
            Name = "Partcounter Standard",
            WidthMm = 148,
            HeightMm = 105,
            IsDefault = true
        };

        t.Elements.AddRange(new[]
        {
            Text(4.8, 3.2, 92, 8, "PARTCOUNTER", 18, bold: true),
            Text(4.8, 11.5, 92, 8, "VE {{VeNumber}} · Maschine {{MachineNumber}}", 16, bold: true, data: true),
            Text(4.8, 20.6, 92, 7, "Artikel: {{ArticleNumber}}", 15, bold: true, data: true),
            Text(4.8, 27.3, 87, 9, "{{ArticleDescription}}", 12, data: true),
            Text(4.8, 35.0, 92, 7, "Auftrag: {{OrderNumber}}", 12, data: true),
            Text(4.8, 41.7, 92, 7, "Werkzeug: {{ToolNumber}} · Kavitäten: {{Cavities}}", 12, data: true),
            Text(4.8, 50.8, 92, 10, "Menge: {{ActualQuantity}} Stück", 22, bold: true, data: true),
            Text(4.8, 61.5, 92, 7, "VE-Soll: {{TargetQuantity}} · Mehrmenge: {{Overfill}}", 11, data: true),
            Text(4.8, 68.0, 92, 7, "Fertig: {{CompletedAt}}", 11, data: true),
            Text(4.8, 74.0, 92, 7, "VE-ID: {{VE_ID}}", 9, data: true),
            new LabelElementDefinition { Type = LabelElementType.QrCode, Xmm = 100.5, Ymm = 6.4, WidthMm = 43.7, HeightMm = 43.7, Content = "{{QrPayload}}", ZIndex = 5 },
            new LabelElementDefinition { Type = LabelElementType.Code128, Xmm = 6.6, Ymm = 81.0, WidthMm = 135, HeightMm = 19, Content = "{{VE_ID}}", ZIndex = 5 }
        });

        for (var i = 0; i < t.Elements.Count; i++)
            t.Elements[i].ZIndex = i;
        return t;
    }

    private static LabelElementDefinition Text(double x, double y, double width, double height, string content,
        double fontSize, bool bold = false, bool data = false) => new()
    {
        Type = data ? LabelElementType.DataText : LabelElementType.Text,
        Xmm = x,
        Ymm = y,
        WidthMm = width,
        HeightMm = height,
        Content = content,
        FontSizePt = fontSize,
        Bold = bold
    };

    private static string BuildQrPayload(PackagingUnitRecord record) =>
        $"PC1|VE={record.Id}|M={record.MachineNumber:00}|A={record.ArticleNumber}|WZ={record.ToolNumber}|Q={record.ActualQuantity}|TS={record.CompletedAtUtc:O}";

    private static void ValidateTemplate(LabelTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
            throw new InvalidOperationException("Vorlagen-ID fehlt.");
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new InvalidOperationException("Vorlagenname darf nicht leer sein.");
        if (template.WidthMm is < 20 or > 500 || template.HeightMm is < 20 or > 500)
            throw new InvalidOperationException("Etikettengröße muss zwischen 20 und 500 mm liegen.");
        if (template.Elements.Count > 250)
            throw new InvalidOperationException("Eine Vorlage darf maximal 250 Elemente enthalten.");

        foreach (var element in template.Elements)
        {
            if (element.Xmm < 0 || element.Ymm < 0 || element.WidthMm <= 0 || element.HeightMm <= 0)
                throw new InvalidOperationException("Elementposition oder -größe ist ungültig.");
            if (element.Xmm + element.WidthMm > template.WidthMm + 0.1 ||
                element.Ymm + element.HeightMm > template.HeightMm + 0.1)
                throw new InvalidOperationException($"Element '{element.Type}' liegt außerhalb des Etiketts.");
        }
    }
}
