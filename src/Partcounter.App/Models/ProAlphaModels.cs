using System.ComponentModel;

namespace Partcounter.Models;

public enum ProAlphaSourceMode
{
    FileExport = 0,
    RestJson = 1
}

public enum ProAlphaAuthenticationType
{
    None = 0,
    Basic = 1,
    Bearer = 2,
    ApiKey = 3,
    OAuth2ClientCredentials = 4,
    OAuth2Password = 5,
    OAuth2RefreshToken = 6
}

public enum ProAlphaProxyMode
{
    SystemDefault = 0,
    None = 1,
    Custom = 2
}

public sealed class ProAlphaConnectionSettings
{
    public bool Enabled { get; set; } = true;
    public ProAlphaSourceMode SourceMode { get; set; } = ProAlphaSourceMode.RestJson;
    public bool AutoPoll { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public string CultureName { get; set; } = "de-DE";

    // File / batch integration
    public string FilePath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*.xlsx";
    public string ExcelSheetName { get; set; } = string.Empty;
    public int HeaderRow { get; set; } = 1;
    public string CsvDelimiter { get; set; } = ";";
    public string FileEncodingName { get; set; } = "utf-8";
    public bool ArchiveAfterImport { get; set; }
    public string ArchiveFolder { get; set; } = string.Empty;
    public string ErrorFolder { get; set; } = string.Empty;

    // REST endpoint
    public string RestUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string AcceptMediaType { get; set; } = "application/json";
    public string ContentMediaType { get; set; } = "application/json";
    public string JsonRootPath { get; set; } = string.Empty;
    public string QueryParameters { get; set; } = string.Empty;
    public string AdditionalHeaders { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 500;

    // Pagination
    public string NextLinkJsonPath { get; set; } = string.Empty;
    public int MaxPages { get; set; } = 20;

    // Authentication
    public ProAlphaAuthenticationType AuthenticationType { get; set; } = ProAlphaAuthenticationType.None;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "X-API-Key";
    public string ApiKeyValue { get; set; } = string.Empty;

    // OAuth2 - deliberately complete because actual proALPHA integration products differ by installation.
    public string OAuthTokenUrl { get; set; } = string.Empty;
    public string OAuthClientId { get; set; } = string.Empty;
    public string OAuthClientSecret { get; set; } = string.Empty;
    public string OAuthScope { get; set; } = string.Empty;
    public string OAuthAudience { get; set; } = string.Empty;
    public string OAuthRefreshToken { get; set; } = string.Empty;
    public bool OAuthClientCredentialsInBasicHeader { get; set; }
    public string OAuthAdditionalParameters { get; set; } = string.Empty;

    // TLS / client certificate
    public bool AllowUntrustedTls { get; set; }
    public string ClientCertificatePath { get; set; } = string.Empty;
    public string ClientCertificatePassword { get; set; } = string.Empty;

    // Proxy
    public ProAlphaProxyMode ProxyMode { get; set; } = ProAlphaProxyMode.SystemDefault;
    public string ProxyUrl { get; set; } = string.Empty;
    public string ProxyUsername { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;

    // Business context. These values can be referenced as {Company}, {Plant}, {Resource}, {Status}
    // in URL, query parameters, headers and POST body.
    public string EnvironmentName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string PlantCode { get; set; } = string.Empty;
    public string ResourceFilter { get; set; } = string.Empty;
    public string OrderStatusFilter { get; set; } = string.Empty;

    // Import behavior
    public bool CreateMissingArticles { get; set; } = true;
    public bool UpdateExistingArticles { get; set; }
    public bool AutoStartOnApply { get; set; }
    public string MachineAliasMap { get; set; } = string.Empty;
}

public sealed class ProAlphaFieldMapping : INotifyPropertyChanged
{
    private string _sourceField = string.Empty;

    public ProAlphaFieldMapping(string targetField, string sourceField, bool required, string description, string example = "")
    {
        TargetField = targetField;
        _sourceField = sourceField;
        Required = required;
        Description = description;
        Example = example;
    }

    public string TargetField { get; }
    public bool Required { get; }
    public string Description { get; }
    public string Example { get; }
    public string SourceField
    {
        get => _sourceField;
        set
        {
            if (_sourceField == value) return;
            _sourceField = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceField)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record ProAlphaOrderRecord(
    int? MachineNumber,
    string MachineName,
    string MachineExternalId,
    string WorkCenter,
    string OrderNumber,
    string OperationNumber,
    string ArticleNumber,
    uint OrderQuantity,
    string ArticleDescription,
    string ToolNumber,
    ushort? Cavities,
    uint? PackagingQuantity,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string OrderStatus,
    string Priority,
    string MaterialNumber,
    string MaterialDescription,
    string Batch,
    string Color,
    string CustomerOrder,
    string CompanyCode,
    string PlantCode,
    DateTime? LastChanged,
    string SourceDescription)
{
    public string MachineDisplay => MachineNumber.HasValue
        ? $"M{MachineNumber.Value:00}"
        : !string.IsNullOrWhiteSpace(WorkCenter)
            ? WorkCenter
            : !string.IsNullOrWhiteSpace(MachineExternalId)
                ? MachineExternalId
                : MachineName;

    public string PlannedStartText => PlannedStart?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
    public string PlannedEndText => PlannedEnd?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
    public string CavitiesText => Cavities?.ToString() ?? string.Empty;
    public string PackagingQuantityText => PackagingQuantity?.ToString("N0") ?? string.Empty;
}

public sealed record IntegrationPreflightIssue(string Field, string Message, bool IsError);

public sealed record IntegrationPreflightResult(IReadOnlyList<IntegrationPreflightIssue> Issues)
{
    public bool IsReady => Issues.All(issue => !issue.IsError);
    public string Summary => IsReady
        ? "Konfiguration vollständig: alle für den gewählten Zugriffsweg erforderlichen Angaben sind vorhanden."
        : $"Konfiguration unvollständig: {Issues.Count(issue => issue.IsError)} Pflichtangabe(n) fehlen oder sind ungültig.";
}
