using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Partcounter.Models;

namespace Partcounter.Services;

/// <summary>
/// Extended commissioning support for customer-specific ALS REST access.
/// Existing ALS file/REST mapping remains unchanged; this service validates all optional access
/// parameters and can obtain an OAuth2 client-credentials token for installations that require it.
/// </summary>
public sealed class AlsExtendedAccessService
{
    public IntegrationPreflightResult Validate(AlsConnectionSettings settings, IEnumerable<AlsFieldMapping> mappings)
    {
        var issues = new List<IntegrationPreflightIssue>();
        void Error(string field, string message) => issues.Add(new(field, message, true));
        void Warn(string field, string message) => issues.Add(new(field, message, false));

        var mapping = mappings.ToDictionary(m => m.TargetField, m => m.SourceField ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "OrderNumber", "ArticleNumber", "OrderQuantity" })
            if (!mapping.TryGetValue(required, out var source) || string.IsNullOrWhiteSpace(source))
                Error(required, $"Pflichtmapping für {required} fehlt.");

        if (settings.SourceMode == AlsSourceMode.FileExport)
        {
            if (string.IsNullOrWhiteSpace(settings.FilePath)) Error("FilePath", "ALS-Datei/Hotfolder fehlt.");
            if (settings.ArchiveAfterImport && string.IsNullOrWhiteSpace(settings.ArchiveFolder)) Error("ArchiveFolder", "Archivierung ist aktiv, Archivordner fehlt.");
            return new IntegrationPreflightResult(issues);
        }

        if (!Uri.TryCreate(settings.RestUrl, UriKind.Absolute, out var endpoint))
            Error("RestUrl", "ALS REST-URL fehlt oder ist ungültig.");
        else if (endpoint.Scheme != Uri.UriSchemeHttps)
            Warn("RestUrl", "ALS REST-Endpunkt verwendet kein HTTPS.");

        switch (settings.AuthenticationType)
        {
            case AlsAuthenticationType.Basic:
                if (string.IsNullOrWhiteSpace(settings.Username)) Error("Username", "Basic-Auth Benutzername fehlt.");
                if (string.IsNullOrWhiteSpace(settings.Password)) Error("Password", "Basic-Auth Passwort fehlt.");
                break;
            case AlsAuthenticationType.Bearer:
                if (string.IsNullOrWhiteSpace(settings.BearerToken)) Error("BearerToken", "Bearer Token fehlt.");
                break;
            case AlsAuthenticationType.ApiKey:
                if (string.IsNullOrWhiteSpace(settings.ApiKeyHeader)) Error("ApiKeyHeader", "API-Key Header fehlt.");
                if (string.IsNullOrWhiteSpace(settings.ApiKeyValue)) Error("ApiKeyValue", "API-Key Wert fehlt.");
                break;
            case AlsAuthenticationType.OAuth2ClientCredentials:
                if (!Uri.TryCreate(settings.OAuthTokenUrl, UriKind.Absolute, out _)) Error("OAuthTokenUrl", "OAuth2 Token-URL fehlt/ist ungültig.");
                if (string.IsNullOrWhiteSpace(settings.OAuthClientId)) Error("OAuthClientId", "OAuth2 Client-ID fehlt.");
                if (string.IsNullOrWhiteSpace(settings.OAuthClientSecret)) Error("OAuthClientSecret", "OAuth2 Client-Secret fehlt.");
                break;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath) && !File.Exists(Environment.ExpandEnvironmentVariables(settings.ClientCertificatePath)))
            Error("ClientCertificatePath", "ALS Client-Zertifikat wurde nicht gefunden.");

        if (settings.ProxyMode == AlsProxyMode.Custom && !Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out _))
            Error("ProxyUrl", "Benutzerdefinierter Proxy ist aktiv, Proxy-URL fehlt/ist ungültig.");

        if (string.IsNullOrWhiteSpace(settings.JsonRootPath))
            Warn("JsonRootPath", "JSON-Wurzelpfad ist leer. Das ist nur korrekt, wenn die Antwort direkt das Auftragsobjekt/-array enthält.");

        return new IntegrationPreflightResult(issues);
    }

    public async Task<string> AcquireOAuthClientCredentialsTokenAsync(AlsConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        var preflight = Validate(settings, Array.Empty<AlsFieldMapping>());
        if (settings.AuthenticationType != AlsAuthenticationType.OAuth2ClientCredentials)
            throw new InvalidOperationException("ALS-Authentifizierung steht nicht auf OAuth2ClientCredentials.");
        if (!Uri.TryCreate(settings.OAuthTokenUrl, UriKind.Absolute, out var tokenUri))
            throw new InvalidOperationException("OAuth2 Token-URL fehlt/ist ungültig.");
        if (string.IsNullOrWhiteSpace(settings.OAuthClientId) || string.IsNullOrWhiteSpace(settings.OAuthClientSecret))
            throw new InvalidOperationException("OAuth2 Client-ID oder Client-Secret fehlt.");

        using var handler = new HttpClientHandler();
        if (settings.ProxyMode == AlsProxyMode.None) handler.UseProxy = false;
        else if (settings.ProxyMode == AlsProxyMode.Custom)
        {
            var proxy = new WebProxy(settings.ProxyUrl);
            if (!string.IsNullOrWhiteSpace(settings.ProxyUsername))
                proxy.Credentials = new NetworkCredential(settings.ProxyUsername, settings.ProxyPassword);
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        if (settings.AllowUntrustedTls)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 2, 300)) };
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri);
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
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
        foreach (var pair in ParseNameValueLines(settings.OAuthAdditionalParameters)) form[pair.Key] = pair.Value;
        request.Content = new FormUrlEncodedContent(form);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ALS OAuth2 Tokenabruf fehlgeschlagen: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {(body.Length > 500 ? body[..500] : body)}");
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
            throw new InvalidOperationException("OAuth2-Antwort enthält kein access_token.");
        return token.GetString()!;
    }

    private static Dictionary<string, string> ParseNameValueLines(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var raw in text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator < 1) continue;
            result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return result;
    }
}
