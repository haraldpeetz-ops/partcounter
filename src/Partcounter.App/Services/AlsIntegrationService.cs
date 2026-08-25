using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class AlsIntegrationService
{
    static AlsIntegrationService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<IReadOnlyList<AlsOrderRecord>> LoadOrdersAsync(
        AlsConnectionSettings settings,
        IEnumerable<AlsFieldMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            throw new InvalidOperationException("Die ALS-Schnittstelle ist deaktiviert.");

        var mapping = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.SourceField))
            .ToDictionary(m => m.TargetField, m => m.SourceField.Trim(), StringComparer.OrdinalIgnoreCase);

        ValidateMappings(mapping);

        return settings.SourceMode switch
        {
            AlsSourceMode.FileExport => await LoadFileOrdersAsync(settings, mapping, cancellationToken),
            AlsSourceMode.RestJson => await LoadRestOrdersAsync(settings, mapping, cancellationToken),
            _ => throw new NotSupportedException($"ALS-Quellmodus {settings.SourceMode} wird nicht unterstützt.")
        };
    }

    public async Task<string> TestConnectionAsync(
        AlsConnectionSettings settings,
        IEnumerable<AlsFieldMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        var orders = await LoadOrdersAsync(settings, mappings, cancellationToken);
        return settings.SourceMode switch
        {
            AlsSourceMode.FileExport => $"Datei-/Hotfolder-Zugriff erfolgreich. {orders.Count:N0} gültige Auftragsdatensätze gelesen.",
            AlsSourceMode.RestJson => $"REST-Aufruf erfolgreich. {orders.Count:N0} gültige Auftragsdatensätze gelesen.",
            _ => $"Verbindung erfolgreich. {orders.Count:N0} Datensätze gelesen."
        };
    }

    private async Task<IReadOnlyList<AlsOrderRecord>> LoadFileOrdersAsync(
        AlsConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        var sourceFile = ResolveSourceFile(settings);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
            IReadOnlyList<AlsOrderRecord> result = extension switch
            {
                ".xlsx" or ".xlsm" => LoadExcelOrders(sourceFile, settings, mapping),
                ".csv" or ".txt" or ".tsv" => await LoadDelimitedOrdersAsync(sourceFile, settings, mapping, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Nicht unterstütztes ALS-Dateiformat '{extension}'. Unterstützt: .xlsx, .xlsm, .csv, .txt, .tsv.")
            };

            if (settings.ArchiveAfterImport)
                MoveToFolder(sourceFile, settings.ArchiveFolder, "archive");

            return result;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(settings.ErrorFolder) && File.Exists(sourceFile))
            {
                try { MoveToFolder(sourceFile, settings.ErrorFolder, "error"); }
                catch { }
            }
            throw;
        }
    }

    private static string ResolveSourceFile(AlsConnectionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FilePath))
            throw new InvalidOperationException("ALS-Dateipfad/Hotfolder ist nicht konfiguriert.");

        var path = Environment.ExpandEnvironmentVariables(settings.FilePath.Trim());
        if (File.Exists(path))
            return Path.GetFullPath(path);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"ALS-Pfad nicht gefunden: {path}");

        var pattern = string.IsNullOrWhiteSpace(settings.FilePattern) ? "*.xlsx" : settings.FilePattern.Trim();
        var file = Directory
            .EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return file ?? throw new FileNotFoundException(
            $"Im ALS-Hotfolder wurde keine Datei für das Muster '{pattern}' gefunden.", path);
    }

    private static IReadOnlyList<AlsOrderRecord> LoadExcelOrders(
        string sourceFile,
        AlsConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping)
    {
        using var workbook = new XLWorkbook(sourceFile);
        var worksheet = string.IsNullOrWhiteSpace(settings.ExcelSheetName)
            ? workbook.Worksheets.FirstOrDefault()
            : workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Equals(settings.ExcelSheetName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (worksheet is null)
            throw new InvalidOperationException("Das konfigurierte Excel-Arbeitsblatt wurde nicht gefunden.");

        var headerRowNumber = Math.Max(1, settings.HeaderRow);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastColumn == 0 || lastRow <= headerRowNumber)
            return Array.Empty<AlsOrderRecord>();

        var headers = new Dictionary<int, string>();
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = worksheet.Cell(headerRowNumber, column).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(header) && !headers.Values.Contains(header, StringComparer.OrdinalIgnoreCase))
                headers[column] = header;
        }

        var culture = GetCulture(settings.CultureName);
        var results = new List<AlsOrderRecord>();
        for (var row = headerRowNumber + 1; row <= lastRow; row++)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headers)
                values[pair.Value] = worksheet.Cell(row, pair.Key).GetFormattedString().Trim();

            if (values.Values.All(string.IsNullOrWhiteSpace))
                continue;

            var record = MapRecord(
                sourceField => values.TryGetValue(sourceField, out var value) ? value : null,
                mapping,
                culture,
                $"{Path.GetFileName(sourceFile)} · {worksheet.Name} · Zeile {row}");

            if (record is not null)
                results.Add(record);
        }

        return Deduplicate(results);
    }

    private static async Task<IReadOnlyList<AlsOrderRecord>> LoadDelimitedOrdersAsync(
        string sourceFile,
        AlsConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        var encodingName = string.IsNullOrWhiteSpace(settings.FileEncodingName) ? "utf-8" : settings.FileEncodingName.Trim();
        Encoding encoding;
        try { encoding = Encoding.GetEncoding(encodingName); }
        catch (Exception ex) { throw new InvalidOperationException($"Unbekannte Dateicodierung '{encodingName}'.", ex); }

        var text = await File.ReadAllTextAsync(sourceFile, encoding, cancellationToken);
        var delimiter = ParseDelimiter(settings.CsvDelimiter, Path.GetExtension(sourceFile));
        var rows = ParseDelimitedText(text, delimiter);
        var headerIndex = Math.Max(0, settings.HeaderRow - 1);
        if (rows.Count <= headerIndex)
            return Array.Empty<AlsOrderRecord>();

        var headers = rows[headerIndex].Select(h => h.Trim()).ToList();
        var culture = GetCulture(settings.CultureName);
        var results = new List<AlsOrderRecord>();

        for (var rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(headers[i])) continue;
                values[headers[i]] = i < row.Count ? row[i].Trim() : string.Empty;
            }

            var record = MapRecord(
                sourceField => values.TryGetValue(sourceField, out var value) ? value : null,
                mapping,
                culture,
                $"{Path.GetFileName(sourceFile)} · Zeile {rowIndex + 1}");

            if (record is not null)
                results.Add(record);
        }

        return Deduplicate(results);
    }

    private async Task<IReadOnlyList<AlsOrderRecord>> LoadRestOrdersAsync(
        AlsConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.RestUrl?.Trim(), UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Die ALS-REST-URL ist leer oder ungültig.");

        using var handler = new HttpClientHandler();
        if (settings.AllowUntrustedTls)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath))
        {
            var certificatePath = Environment.ExpandEnvironmentVariables(settings.ClientCertificatePath.Trim());
            if (!File.Exists(certificatePath))
                throw new FileNotFoundException("ALS-Clientzertifikat nicht gefunden.", certificatePath);

            handler.ClientCertificates.Add(new X509Certificate2(certificatePath, settings.ClientCertificatePassword));
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 2, 300))
        };

        var method = string.Equals(settings.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            ? HttpMethod.Post
            : HttpMethod.Get;
        using var request = new HttpRequestMessage(method, uri);
        ApplyAuthentication(request, settings);
        ApplyAdditionalHeaders(request, settings.AdditionalHeaders);

        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(
                string.IsNullOrWhiteSpace(settings.RequestBody) ? "{}" : settings.RequestBody,
                Encoding.UTF8,
                "application/json");
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var excerpt = responseBody.Length > 500 ? responseBody[..500] : responseBody;
            throw new InvalidOperationException(
                $"ALS-REST-Aufruf fehlgeschlagen: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {excerpt}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = ResolveJsonPath(document.RootElement, settings.JsonRootPath);
        var elements = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray().ToList(),
            JsonValueKind.Object => new List<JsonElement> { root },
            _ => throw new InvalidOperationException("Der konfigurierte JSON-Wurzelpfad liefert weder ein Objekt noch ein Array.")
        };

        var culture = GetCulture(settings.CultureName);
        var results = new List<AlsOrderRecord>();
        var index = 0;
        foreach (var element in elements)
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            var record = MapRecord(
                sourceField => ReadJsonPathAsString(element, sourceField),
                mapping,
                culture,
                $"REST {uri.Host} · Datensatz {index}");

            if (record is not null)
                results.Add(record);
        }

        return Deduplicate(results);
    }

    private static void ApplyAuthentication(HttpRequestMessage request, AlsConnectionSettings settings)
    {
        switch (settings.AuthenticationType)
        {
            case AlsAuthenticationType.Basic:
            {
                var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
                break;
            }
            case AlsAuthenticationType.Bearer:
                if (!string.IsNullOrWhiteSpace(settings.BearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken.Trim());
                break;
            case AlsAuthenticationType.ApiKey:
                if (!string.IsNullOrWhiteSpace(settings.ApiKeyHeader) && !string.IsNullOrWhiteSpace(settings.ApiKeyValue))
                    request.Headers.TryAddWithoutValidation(settings.ApiKeyHeader.Trim(), settings.ApiKeyValue.Trim());
                break;
        }
    }

    private static void ApplyAdditionalHeaders(HttpRequestMessage request, string? headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText)) return;

        foreach (var rawLine in headerText.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var separator = line.IndexOf(':');
            if (separator < 1) separator = line.IndexOf('=');
            if (separator < 1) continue;

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(name))
                request.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static JsonElement ResolveJsonPath(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out current))
                throw new InvalidOperationException($"JSON-Wurzelpfad '{path}' wurde nicht gefunden (Segment '{segment}').");
        }
        return current;
    }

    private static string? ReadJsonPathAsString(JsonElement element, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var current = element;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static AlsOrderRecord? MapRecord(
        Func<string, string?> getSourceValue,
        IReadOnlyDictionary<string, string> mapping,
        CultureInfo culture,
        string sourceDescription)
    {
        string? Get(string target) => mapping.TryGetValue(target, out var source) ? getSourceValue(source)?.Trim() : null;

        var orderNumber = Get("OrderNumber") ?? string.Empty;
        var articleNumber = Get("ArticleNumber") ?? string.Empty;
        var orderQuantity = ParseUInt(Get("OrderQuantity"), culture);
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(articleNumber) || orderQuantity == 0)
            return null;

        return new AlsOrderRecord(
            ParseNullableInt(Get("MachineNumber"), culture),
            Get("MachineName") ?? string.Empty,
            Get("MachineExternalId") ?? string.Empty,
            orderNumber,
            articleNumber,
            orderQuantity,
            Get("ArticleDescription") ?? string.Empty,
            Get("ToolNumber") ?? string.Empty,
            ParseNullableUShort(Get("Cavities"), culture),
            ParseNullableUInt(Get("PackagingQuantity"), culture),
            ParseNullableDate(Get("PlannedStart"), culture),
            ParseNullableDate(Get("PlannedEnd"), culture),
            Get("OrderStatus") ?? string.Empty,
            Get("OperationNumber") ?? string.Empty,
            Get("Priority") ?? string.Empty,
            Get("MaterialNumber") ?? string.Empty,
            Get("MaterialDescription") ?? string.Empty,
            Get("Batch") ?? string.Empty,
            Get("Color") ?? string.Empty,
            Get("CustomerOrder") ?? string.Empty,
            ParseNullableDate(Get("LastChanged"), culture),
            sourceDescription);
    }

    private static IReadOnlyList<AlsOrderRecord> Deduplicate(IEnumerable<AlsOrderRecord> records) => records
        .GroupBy(r => $"{r.OrderNumber}|{r.OperationNumber}|{r.MachineNumber}|{r.MachineExternalId}", StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(x => x.LastChanged ?? DateTime.MinValue).First())
        .OrderBy(r => r.PlannedStart ?? DateTime.MaxValue)
        .ThenBy(r => r.MachineNumber ?? int.MaxValue)
        .ThenBy(r => r.OrderNumber, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static void ValidateMappings(IReadOnlyDictionary<string, string> mapping)
    {
        foreach (var required in new[] { "OrderNumber", "ArticleNumber", "OrderQuantity" })
        {
            if (!mapping.TryGetValue(required, out var source) || string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException($"Im ALS-Feldmapping fehlt das Pflichtfeld '{required}'.");
        }

        if (!mapping.ContainsKey("MachineNumber") && !mapping.ContainsKey("MachineName") && !mapping.ContainsKey("MachineExternalId"))
            throw new InvalidOperationException("Im ALS-Feldmapping muss mindestens MachineNumber, MachineName oder MachineExternalId belegt sein.");
    }

    private static CultureInfo GetCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return CultureInfo.InvariantCulture;
        try { return CultureInfo.GetCultureInfo(cultureName.Trim()); }
        catch { return CultureInfo.InvariantCulture; }
    }

    private static uint ParseUInt(string? value, CultureInfo culture)
        => ParseNullableUInt(value, culture) ?? 0;

    private static uint? ParseNullableUInt(string? value, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (uint.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, culture, out var direct)) return direct;
        if (decimal.TryParse(value, NumberStyles.Number, culture, out var decimalValue) && decimalValue >= 0 && decimalValue <= uint.MaxValue)
            return (uint)Math.Round(decimalValue, MidpointRounding.AwayFromZero);
        return null;
    }

    private static int? ParseNullableInt(string? value, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, culture, out var result) ? result : null;
    }

    private static ushort? ParseNullableUShort(string? value, CultureInfo culture)
    {
        var number = ParseNullableUInt(value, culture);
        return number is > 0 and <= ushort.MaxValue ? (ushort)number.Value : null;
    }

    private static DateTime? ParseNullableDate(string? value, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, culture, DateTimeStyles.AssumeLocal, out var local)) return local;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var invariant)) return invariant;
        return null;
    }

    private static char ParseDelimiter(string? configured, string extension)
    {
        if (string.Equals(configured, "\\t", StringComparison.OrdinalIgnoreCase) || configured == "\t") return '\t';
        if (!string.IsNullOrEmpty(configured)) return configured[0];
        return extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ';';
    }

    private static List<List<string>> ParseDelimitedText(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r')
            {
                // wait for optional LF
            }
            else if (c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static void MoveToFolder(string sourceFile, string? configuredFolder, string suffix)
    {
        if (string.IsNullOrWhiteSpace(configuredFolder))
            throw new InvalidOperationException($"Archivierung ist aktiviert, aber kein {suffix}-Ordner ist konfiguriert.");

        var folder = Environment.ExpandEnvironmentVariables(configuredFolder.Trim());
        Directory.CreateDirectory(folder);
        var target = Path.Combine(
            folder,
            $"{Path.GetFileNameWithoutExtension(sourceFile)}_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}{Path.GetExtension(sourceFile)}");
        File.Move(sourceFile, target, overwrite: false);
    }
}
