using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class ProAlphaIntegrationService
{
    static ProAlphaIntegrationService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public IntegrationPreflightResult ValidateConfiguration(
        ProAlphaConnectionSettings settings,
        IEnumerable<ProAlphaFieldMapping> mappings)
    {
        var issues = new List<IntegrationPreflightIssue>();
        void Error(string field, string message) => issues.Add(new(field, message, true));
        void Warn(string field, string message) => issues.Add(new(field, message, false));

        if (!settings.Enabled)
            Warn("Enabled", "proALPHA ist derzeit deaktiviert.");

        var map = mappings.ToDictionary(m => m.TargetField, m => m.SourceField ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "OrderNumber", "ArticleNumber", "OrderQuantity" })
        {
            if (!map.TryGetValue(required, out var source) || string.IsNullOrWhiteSpace(source))
                Error(required, $"Pflichtmapping für {required} fehlt.");
        }

        var machineMappings = new[] { "MachineNumber", "MachineName", "MachineExternalId", "WorkCenter" };
        if (!machineMappings.Any(key => map.TryGetValue(key, out var source) && !string.IsNullOrWhiteSpace(source)))
            Warn("Maschinenzuordnung", "Keine Maschinen-/Arbeitsplatzkennung gemappt. Auftrag kann später nur manuell einer Maschine zugeordnet werden.");

        if (settings.SourceMode == ProAlphaSourceMode.FileExport)
        {
            if (string.IsNullOrWhiteSpace(settings.FilePath))
                Error("FilePath", "Datei oder Hotfolder fehlt.");
            if (settings.HeaderRow < 1)
                Error("HeaderRow", "Kopfzeile muss mindestens 1 sein.");
            if (settings.ArchiveAfterImport && string.IsNullOrWhiteSpace(settings.ArchiveFolder))
                Error("ArchiveFolder", "Archivierung ist aktiv, aber kein Archivordner ist angegeben.");
        }
        else
        {
            if (!Uri.TryCreate(ExpandTokens(settings.RestUrl, settings), UriKind.Absolute, out var endpoint))
                Error("RestUrl", "REST-Endpunkt fehlt oder ist keine absolute URL.");
            else if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                Warn("RestUrl", "REST-Endpunkt verwendet kein HTTPS. Für Produktionsbetrieb wird HTTPS empfohlen.");

            if (settings.TimeoutSeconds is < 2 or > 300)
                Error("TimeoutSeconds", "Timeout muss zwischen 2 und 300 Sekunden liegen.");
            if (settings.MaxPages is < 1 or > 500)
                Error("MaxPages", "MaxPages muss zwischen 1 und 500 liegen.");

            switch (settings.AuthenticationType)
            {
                case ProAlphaAuthenticationType.Basic:
                    if (string.IsNullOrWhiteSpace(settings.Username)) Error("Username", "Benutzername für Basic Auth fehlt.");
                    if (string.IsNullOrWhiteSpace(settings.Password)) Error("Password", "Passwort für Basic Auth fehlt.");
                    break;
                case ProAlphaAuthenticationType.Bearer:
                    if (string.IsNullOrWhiteSpace(settings.BearerToken)) Error("BearerToken", "Bearer Token fehlt.");
                    break;
                case ProAlphaAuthenticationType.ApiKey:
                    if (string.IsNullOrWhiteSpace(settings.ApiKeyHeader)) Error("ApiKeyHeader", "API-Key Headername fehlt.");
                    if (string.IsNullOrWhiteSpace(settings.ApiKeyValue)) Error("ApiKeyValue", "API-Key Wert fehlt.");
                    break;
                case ProAlphaAuthenticationType.OAuth2ClientCredentials:
                    ValidateOAuthBase(settings, Error);
                    break;
                case ProAlphaAuthenticationType.OAuth2Password:
                    ValidateOAuthBase(settings, Error);
                    if (string.IsNullOrWhiteSpace(settings.Username)) Error("Username", "OAuth2-Benutzername fehlt.");
                    if (string.IsNullOrWhiteSpace(settings.Password)) Error("Password", "OAuth2-Passwort fehlt.");
                    break;
                case ProAlphaAuthenticationType.OAuth2RefreshToken:
                    ValidateOAuthBase(settings, Error);
                    if (string.IsNullOrWhiteSpace(settings.OAuthRefreshToken)) Error("OAuthRefreshToken", "OAuth2 Refresh Token fehlt.");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath) && !File.Exists(Environment.ExpandEnvironmentVariables(settings.ClientCertificatePath)))
                Error("ClientCertificatePath", "Client-Zertifikatdatei wurde nicht gefunden.");

            if (settings.ProxyMode == ProAlphaProxyMode.Custom)
            {
                if (!Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out _))
                    Error("ProxyUrl", "Benutzerdefinierter Proxy ist aktiv, aber die Proxy-URL fehlt/ist ungültig.");
            }
        }

        if (string.IsNullOrWhiteSpace(settings.CompanyCode))
            Warn("CompanyCode", "Mandant/Firma ist leer. Das ist nur dann korrekt, wenn der proALPHA-Endpunkt keinen Firmenkontext verlangt.");
        if (string.IsNullOrWhiteSpace(settings.PlantCode))
            Warn("PlantCode", "Werk/Standort ist leer. Das ist nur dann korrekt, wenn die API keinen Werkfilter verlangt.");

        return new IntegrationPreflightResult(issues);
    }

    public async Task<string> TestConnectionAsync(
        ProAlphaConnectionSettings settings,
        IEnumerable<ProAlphaFieldMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        var preflight = ValidateConfiguration(settings, mappings);
        if (!preflight.IsReady)
            throw new InvalidOperationException(preflight.Summary + " " + string.Join(" | ", preflight.Issues.Where(i => i.IsError).Select(i => i.Message)));

        var orders = await LoadOrdersAsync(settings, mappings, cancellationToken);
        return settings.SourceMode == ProAlphaSourceMode.FileExport
            ? $"proALPHA Datei-/Hotfolder-Zugriff erfolgreich. {orders.Count:N0} gültige Auftragsdatensätze gelesen."
            : $"proALPHA REST-Aufruf erfolgreich. {orders.Count:N0} gültige Auftragsdatensätze gelesen.";
    }

    public async Task<IReadOnlyList<ProAlphaOrderRecord>> LoadOrdersAsync(
        ProAlphaConnectionSettings settings,
        IEnumerable<ProAlphaFieldMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            throw new InvalidOperationException("Die proALPHA-Schnittstelle ist deaktiviert.");

        var preflight = ValidateConfiguration(settings, mappings);
        if (!preflight.IsReady)
            throw new InvalidOperationException(preflight.Summary + " " + string.Join(" | ", preflight.Issues.Where(i => i.IsError).Select(i => i.Message)));

        var mapping = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.SourceField))
            .ToDictionary(m => m.TargetField, m => m.SourceField.Trim(), StringComparer.OrdinalIgnoreCase);

        return settings.SourceMode switch
        {
            ProAlphaSourceMode.FileExport => await LoadFileOrdersAsync(settings, mapping, cancellationToken),
            ProAlphaSourceMode.RestJson => await LoadRestOrdersAsync(settings, mapping, cancellationToken),
            _ => throw new NotSupportedException($"proALPHA-Quellmodus {settings.SourceMode} wird nicht unterstützt.")
        };
    }

    private static void ValidateOAuthBase(ProAlphaConnectionSettings settings, Action<string, string> error)
    {
        if (!Uri.TryCreate(settings.OAuthTokenUrl, UriKind.Absolute, out _)) error("OAuthTokenUrl", "OAuth2 Token-URL fehlt/ist ungültig.");
        if (string.IsNullOrWhiteSpace(settings.OAuthClientId)) error("OAuthClientId", "OAuth2 Client-ID fehlt.");
        if (string.IsNullOrWhiteSpace(settings.OAuthClientSecret)) error("OAuthClientSecret", "OAuth2 Client-Secret fehlt.");
    }

    private async Task<IReadOnlyList<ProAlphaOrderRecord>> LoadFileOrdersAsync(
        ProAlphaConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        var sourceFile = ResolveSourceFile(settings);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
            IReadOnlyList<ProAlphaOrderRecord> result = extension switch
            {
                ".xlsx" or ".xlsm" => LoadExcelOrders(sourceFile, settings, mapping),
                ".csv" or ".txt" or ".tsv" => await LoadDelimitedOrdersAsync(sourceFile, settings, mapping, cancellationToken),
                _ => throw new InvalidOperationException($"Nicht unterstütztes proALPHA-Dateiformat '{extension}'.")
            };

            if (settings.ArchiveAfterImport)
                MoveToFolder(sourceFile, settings.ArchiveFolder, "archive");
            return result;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(settings.ErrorFolder) && File.Exists(sourceFile))
            {
                try { MoveToFolder(sourceFile, settings.ErrorFolder, "error"); } catch { }
            }
            throw;
        }
    }

    private static string ResolveSourceFile(ProAlphaConnectionSettings settings)
    {
        var path = Environment.ExpandEnvironmentVariables(settings.FilePath.Trim());
        if (File.Exists(path)) return Path.GetFullPath(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"proALPHA-Pfad nicht gefunden: {path}");
        var pattern = string.IsNullOrWhiteSpace(settings.FilePattern) ? "*.xlsx" : settings.FilePattern.Trim();
        return Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? throw new FileNotFoundException($"Keine proALPHA-Datei für Muster '{pattern}' gefunden.", path);
    }

    private static IReadOnlyList<ProAlphaOrderRecord> LoadExcelOrders(
        string sourceFile,
        ProAlphaConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping)
    {
        using var workbook = new XLWorkbook(sourceFile);
        var worksheet = string.IsNullOrWhiteSpace(settings.ExcelSheetName)
            ? workbook.Worksheets.FirstOrDefault()
            : workbook.Worksheets.FirstOrDefault(w => w.Name.Equals(settings.ExcelSheetName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (worksheet is null) throw new InvalidOperationException("Das konfigurierte proALPHA-Excelblatt wurde nicht gefunden.");

        var headerRow = Math.Max(1, settings.HeaderRow);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastColumn == 0 || lastRow <= headerRow) return Array.Empty<ProAlphaOrderRecord>();

        var headers = new Dictionary<int, string>();
        for (var column = 1; column <= lastColumn; column++)
        {
            var value = worksheet.Cell(headerRow, column).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(value)) headers[column] = value;
        }

        var culture = GetCulture(settings.CultureName);
        var results = new List<ProAlphaOrderRecord>();
        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            var values = headers.ToDictionary(p => p.Value, p => worksheet.Cell(row, p.Key).GetFormattedString().Trim(), StringComparer.OrdinalIgnoreCase);
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var record = MapRecord(field => values.TryGetValue(field, out var value) ? value : null, mapping, culture,
                $"{Path.GetFileName(sourceFile)} · {worksheet.Name} · Zeile {row}");
            if (record is not null) results.Add(record);
        }
        return Deduplicate(results);
    }

    private static async Task<IReadOnlyList<ProAlphaOrderRecord>> LoadDelimitedOrdersAsync(
        string sourceFile,
        ProAlphaConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        Encoding encoding;
        try { encoding = Encoding.GetEncoding(string.IsNullOrWhiteSpace(settings.FileEncodingName) ? "utf-8" : settings.FileEncodingName); }
        catch (Exception ex) { throw new InvalidOperationException($"Unbekannte Dateicodierung '{settings.FileEncodingName}'.", ex); }

        var rows = ParseDelimitedText(await File.ReadAllTextAsync(sourceFile, encoding, cancellationToken), ParseDelimiter(settings.CsvDelimiter, Path.GetExtension(sourceFile)));
        var headerIndex = Math.Max(0, settings.HeaderRow - 1);
        if (rows.Count <= headerIndex) return Array.Empty<ProAlphaOrderRecord>();
        var headers = rows[headerIndex].Select(x => x.Trim()).ToList();
        var culture = GetCulture(settings.CultureName);
        var results = new List<ProAlphaOrderRecord>();
        for (var rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++) if (!string.IsNullOrWhiteSpace(headers[i])) values[headers[i]] = i < rows[rowIndex].Count ? rows[rowIndex][i].Trim() : string.Empty;
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var record = MapRecord(field => values.TryGetValue(field, out var value) ? value : null, mapping, culture,
                $"{Path.GetFileName(sourceFile)} · Zeile {rowIndex + 1}");
            if (record is not null) results.Add(record);
        }
        return Deduplicate(results);
    }

    private async Task<IReadOnlyList<ProAlphaOrderRecord>> LoadRestOrdersAsync(
        ProAlphaConnectionSettings settings,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        using var handler = BuildHandler(settings);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 2, 300)) };
        var accessToken = await ResolveAccessTokenAsync(client, settings, cancellationToken);
        var currentUri = BuildRequestUri(settings.RestUrl, settings.QueryParameters, settings);
        var results = new List<ProAlphaOrderRecord>();
        var culture = GetCulture(settings.CultureName);
        var page = 0;

        while (currentUri is not null && page < Math.Clamp(settings.MaxPages, 1, 500))
        {
            page++;
            using var response = await SendWithRetryAsync(client, () => BuildRequest(currentUri, settings, accessToken), settings, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var excerpt = body.Length > 700 ? body[..700] : body;
                throw new InvalidOperationException($"proALPHA REST-Aufruf fehlgeschlagen: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {excerpt}");
            }

            using var document = JsonDocument.Parse(body);
            var root = ResolveJsonPath(document.RootElement, settings.JsonRootPath);
            var elements = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().ToList(),
                JsonValueKind.Object => new List<JsonElement> { root },
                _ => throw new InvalidOperationException("Der konfigurierte proALPHA JSON-Wurzelpfad liefert weder Objekt noch Array.")
            };

            var index = 0;
            foreach (var element in elements)
            {
                index++;
                if (element.ValueKind != JsonValueKind.Object) continue;
                var record = MapRecord(field => ReadJsonPathAsString(element, field), mapping, culture,
                    $"proALPHA REST {currentUri.Host} · Seite {page} · Datensatz {index}");
                if (record is not null) results.Add(record);
            }

            if (string.IsNullOrWhiteSpace(settings.NextLinkJsonPath)) break;
            var next = ReadJsonPathAsString(document.RootElement, settings.NextLinkJsonPath);
            if (string.IsNullOrWhiteSpace(next)) break;
            currentUri = Uri.TryCreate(next, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(currentUri, next);
        }

        return Deduplicate(results);
    }

    private static HttpClientHandler BuildHandler(ProAlphaConnectionSettings settings)
    {
        var handler = new HttpClientHandler();
        if (settings.AllowUntrustedTls)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath))
        {
            var path = Environment.ExpandEnvironmentVariables(settings.ClientCertificatePath.Trim());
            var contentType = X509Certificate2.GetCertContentType(path);
            using var certificate = contentType == X509ContentType.Pfx
                ? X509CertificateLoader.LoadPkcs12FromFile(
                    path,
                    settings.ClientCertificatePassword ?? string.Empty,
                    X509KeyStorageFlags.EphemeralKeySet)
                : X509CertificateLoader.LoadCertificateFromFile(path);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Das proALPHA-Clientzertifikat enthält keinen privaten Schlüssel. Für mTLS ist eine PFX/P12-Datei mit privatem Schlüssel erforderlich.");
            handler.ClientCertificates.Add(certificate);
        }

        switch (settings.ProxyMode)
        {
            case ProAlphaProxyMode.None:
                handler.UseProxy = false;
                break;
            case ProAlphaProxyMode.Custom:
                handler.UseProxy = true;
                var proxy = new WebProxy(settings.ProxyUrl);
                if (!string.IsNullOrWhiteSpace(settings.ProxyUsername))
                    proxy.Credentials = new NetworkCredential(settings.ProxyUsername, settings.ProxyPassword);
                handler.Proxy = proxy;
                break;
            default:
                handler.UseProxy = true;
                break;
        }
        return handler;
    }

    private async Task<string?> ResolveAccessTokenAsync(HttpClient client, ProAlphaConnectionSettings settings, CancellationToken cancellationToken)
    {
        if (settings.AuthenticationType == ProAlphaAuthenticationType.Bearer)
            return settings.BearerToken.Trim();
        if (settings.AuthenticationType is not (ProAlphaAuthenticationType.OAuth2ClientCredentials or ProAlphaAuthenticationType.OAuth2Password or ProAlphaAuthenticationType.OAuth2RefreshToken))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.OAuthTokenUrl);
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (settings.AuthenticationType)
        {
            case ProAlphaAuthenticationType.OAuth2ClientCredentials:
                form["grant_type"] = "client_credentials";
                break;
            case ProAlphaAuthenticationType.OAuth2Password:
                form["grant_type"] = "password";
                form["username"] = settings.Username;
                form["password"] = settings.Password;
                break;
            case ProAlphaAuthenticationType.OAuth2RefreshToken:
                form["grant_type"] = "refresh_token";
                form["refresh_token"] = settings.OAuthRefreshToken;
                break;
        }

        if (settings.OAuthClientCredentialsInBasicHeader)
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.OAuthClientId}:{settings.OAuthClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        else
        {
            form["client_id"] = settings.OAuthClientId;
            form["client_secret"] = settings.OAuthClientSecret;
        }
        if (!string.IsNullOrWhiteSpace(settings.OAuthScope)) form["scope"] = settings.OAuthScope;
        if (!string.IsNullOrWhiteSpace(settings.OAuthAudience)) form["audience"] = settings.OAuthAudience;
        foreach (var pair in ParseNameValueLines(settings.OAuthAdditionalParameters)) form[pair.Key] = ExpandTokens(pair.Value, settings);
        request.Content = new FormUrlEncodedContent(form);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OAuth2 Tokenabruf fehlgeschlagen: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {(body.Length > 500 ? body[..500] : body)}");
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
            throw new InvalidOperationException("OAuth2-Antwort enthält kein access_token.");
        return token.GetString();
    }

    private static HttpRequestMessage BuildRequest(Uri uri, ProAlphaConnectionSettings settings, string? accessToken)
    {
        var method = string.Equals(settings.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get;
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(settings.AcceptMediaType))
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(settings.AcceptMediaType));

        switch (settings.AuthenticationType)
        {
            case ProAlphaAuthenticationType.Basic:
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}")));
                break;
            case ProAlphaAuthenticationType.Bearer:
            case ProAlphaAuthenticationType.OAuth2ClientCredentials:
            case ProAlphaAuthenticationType.OAuth2Password:
            case ProAlphaAuthenticationType.OAuth2RefreshToken:
                if (!string.IsNullOrWhiteSpace(accessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                break;
            case ProAlphaAuthenticationType.ApiKey:
                request.Headers.TryAddWithoutValidation(settings.ApiKeyHeader.Trim(), settings.ApiKeyValue.Trim());
                break;
        }

        foreach (var pair in ParseNameValueLines(ExpandTokens(settings.AdditionalHeaders, settings), true))
            request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);

        if (method == HttpMethod.Post)
            request.Content = new StringContent(ExpandTokens(string.IsNullOrWhiteSpace(settings.RequestBody) ? "{}" : settings.RequestBody, settings), Encoding.UTF8,
                string.IsNullOrWhiteSpace(settings.ContentMediaType) ? "application/json" : settings.ContentMediaType);
        return request;
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        ProAlphaConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(settings.RetryCount, 0, 10) + 1;
        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = requestFactory();
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode < 500 && response.StatusCode is not HttpStatusCode.RequestTimeout && (int)response.StatusCode != 429)
                    return response;
                if (attempt == maxAttempts) return response;
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastException = ex;
            }
            await Task.Delay(Math.Clamp(settings.RetryDelayMilliseconds, 50, 10000) * attempt, cancellationToken);
        }
        throw lastException ?? new HttpRequestException("proALPHA REST-Aufruf nach Wiederholungen fehlgeschlagen.");
    }

    private static Uri BuildRequestUri(string restUrl, string queryText, ProAlphaConnectionSettings settings)
    {
        var baseText = ExpandTokens(restUrl, settings);
        var builder = new UriBuilder(baseText);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query)) query.Add(builder.Query.TrimStart('?'));
        foreach (var pair in ParseNameValueLines(ExpandTokens(queryText, settings)))
            query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        builder.Query = string.Join("&", query.Where(x => !string.IsNullOrWhiteSpace(x)));
        return builder.Uri;
    }

    private static string ExpandTokens(string? text, ProAlphaConnectionSettings settings)
    {
        return (text ?? string.Empty)
            .Replace("{Company}", settings.CompanyCode ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Plant}", settings.PlantCode ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Resource}", settings.ResourceFilter ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Status}", settings.OrderStatusFilter ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseNameValueLines(string? text, bool allowColon = false)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var raw in text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (allowColon && separator < 1) separator = line.IndexOf(':');
            if (separator < 1) continue;
            result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return result;
    }

    private static ProAlphaOrderRecord? MapRecord(
        Func<string, string?> getSourceValue,
        IReadOnlyDictionary<string, string> mapping,
        CultureInfo culture,
        string sourceDescription)
    {
        string? Get(string target) => mapping.TryGetValue(target, out var source) ? getSourceValue(source)?.Trim() : null;
        var order = Get("OrderNumber") ?? string.Empty;
        var article = Get("ArticleNumber") ?? string.Empty;
        var quantity = ParseUInt(Get("OrderQuantity"), culture);
        if (string.IsNullOrWhiteSpace(order) || string.IsNullOrWhiteSpace(article) || quantity == 0) return null;

        return new ProAlphaOrderRecord(
            ParseNullableInt(Get("MachineNumber"), culture),
            Get("MachineName") ?? string.Empty,
            Get("MachineExternalId") ?? string.Empty,
            Get("WorkCenter") ?? string.Empty,
            order,
            Get("OperationNumber") ?? string.Empty,
            article,
            quantity,
            Get("ArticleDescription") ?? string.Empty,
            Get("ToolNumber") ?? string.Empty,
            ParseNullableUShort(Get("Cavities"), culture),
            ParseNullableUInt(Get("PackagingQuantity"), culture),
            ParseNullableDate(Get("PlannedStart"), culture),
            ParseNullableDate(Get("PlannedEnd"), culture),
            Get("OrderStatus") ?? string.Empty,
            Get("Priority") ?? string.Empty,
            Get("MaterialNumber") ?? string.Empty,
            Get("MaterialDescription") ?? string.Empty,
            Get("Batch") ?? string.Empty,
            Get("Color") ?? string.Empty,
            Get("CustomerOrder") ?? string.Empty,
            Get("CompanyCode") ?? string.Empty,
            Get("PlantCode") ?? string.Empty,
            ParseNullableDate(Get("LastChanged"), culture),
            sourceDescription);
    }

    private static IReadOnlyList<ProAlphaOrderRecord> Deduplicate(IEnumerable<ProAlphaOrderRecord> records) => records
        .GroupBy(r => $"{r.OrderNumber}|{r.OperationNumber}|{r.WorkCenter}|{r.MachineExternalId}", StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(x => x.LastChanged ?? DateTime.MinValue).First())
        .OrderBy(r => r.PlannedStart ?? DateTime.MaxValue)
        .ThenBy(r => r.OrderNumber, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static JsonElement ResolveJsonPath(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out current))
                throw new InvalidOperationException($"JSON-Pfad '{path}' wurde nicht gefunden (Segment '{segment}').");
        }
        return current;
    }

    private static string? ReadJsonPathAsString(JsonElement element, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var current = element;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out current)) return null;
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
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        }
        value = default;
        return false;
    }

    private static CultureInfo GetCulture(string? name)
    {
        try { return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(name) ? "de-DE" : name.Trim()); }
        catch { return CultureInfo.GetCultureInfo("de-DE"); }
    }

    private static uint ParseUInt(string? text, CultureInfo culture) =>
        uint.TryParse(text, NumberStyles.Any, culture, out var value) ? value : 0;
    private static uint? ParseNullableUInt(string? text, CultureInfo culture) =>
        uint.TryParse(text, NumberStyles.Any, culture, out var value) ? value : null;
    private static ushort? ParseNullableUShort(string? text, CultureInfo culture) =>
        ushort.TryParse(text, NumberStyles.Any, culture, out var value) ? value : null;
    private static int? ParseNullableInt(string? text, CultureInfo culture) =>
        int.TryParse(text, NumberStyles.Any, culture, out var value) ? value : null;
    private static DateTime? ParseNullableDate(string? text, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTime.TryParse(text, culture, DateTimeStyles.AssumeLocal, out var value)) return value;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)) return value;
        return null;
    }

    private static char ParseDelimiter(string? value, string extension)
    {
        if (string.Equals(value, "\\t", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "tab", StringComparison.OrdinalIgnoreCase)) return '\t';
        if (!string.IsNullOrEmpty(value)) return value[0];
        return extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ';';
    }

    private static List<List<string>> ParseDelimitedText(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == delimiter && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static void MoveToFolder(string sourceFile, string targetFolder, string suffix)
    {
        if (string.IsNullOrWhiteSpace(targetFolder)) throw new InvalidOperationException($"Zielordner für {suffix} fehlt.");
        targetFolder = Environment.ExpandEnvironmentVariables(targetFolder.Trim());
        Directory.CreateDirectory(targetFolder);
        var target = Path.Combine(targetFolder, Path.GetFileName(sourceFile));
        if (File.Exists(target)) target = Path.Combine(targetFolder, $"{Path.GetFileNameWithoutExtension(sourceFile)}_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}{Path.GetExtension(sourceFile)}");
        File.Move(sourceFile, target);
    }
}
