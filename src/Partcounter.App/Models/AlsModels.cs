using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Partcounter.Models;

public enum AlsSourceMode
{
    FileExport = 0,
    RestJson = 1
}

public enum AlsAuthenticationType
{
    None = 0,
    Basic = 1,
    Bearer = 2,
    ApiKey = 3
}

public sealed class AlsConnectionSettings
{
    public bool Enabled { get; set; } = true;
    public AlsSourceMode SourceMode { get; set; } = AlsSourceMode.FileExport;
    public bool AutoPoll { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;

    public string FilePath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*.xlsx";
    public string ExcelSheetName { get; set; } = string.Empty;
    public int HeaderRow { get; set; } = 1;
    public string CsvDelimiter { get; set; } = ";";
    public string FileEncodingName { get; set; } = "utf-8";
    public string CultureName { get; set; } = "de-DE";
    public bool ArchiveAfterImport { get; set; }
    public string ArchiveFolder { get; set; } = string.Empty;
    public string ErrorFolder { get; set; } = string.Empty;

    public string RestUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public AlsAuthenticationType AuthenticationType { get; set; } = AlsAuthenticationType.None;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "X-API-Key";
    public string ApiKeyValue { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public string JsonRootPath { get; set; } = string.Empty;
    public string AdditionalHeaders { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
    public bool AllowUntrustedTls { get; set; }
    public string ClientCertificatePath { get; set; } = string.Empty;
    public string ClientCertificatePassword { get; set; } = string.Empty;

    public bool CreateMissingArticles { get; set; } = true;
    public bool UpdateExistingArticles { get; set; }
    public bool AutoStartOnApply { get; set; }
    public string MachineAliasMap { get; set; } = string.Empty;
}

public sealed class AlsFieldMapping : INotifyPropertyChanged
{
    private string _sourceField = string.Empty;

    public AlsFieldMapping(string targetField, string sourceField, bool required, string description, string example = "")
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

public sealed record AlsOrderRecord(
    int? MachineNumber,
    string MachineName,
    string MachineExternalId,
    string OrderNumber,
    string ArticleNumber,
    uint OrderQuantity,
    string ArticleDescription,
    string ToolNumber,
    ushort? Cavities,
    uint? PackagingQuantity,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string OrderStatus,
    string OperationNumber,
    string Priority,
    string MaterialNumber,
    string MaterialDescription,
    string Batch,
    string Color,
    string CustomerOrder,
    DateTime? LastChanged,
    string SourceDescription)
{
    public string MachineDisplay => MachineNumber.HasValue
        ? $"M{MachineNumber.Value:00}"
        : !string.IsNullOrWhiteSpace(MachineExternalId)
            ? MachineExternalId
            : MachineName;

    public string PlannedStartText => PlannedStart?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
    public string PlannedEndText => PlannedEnd?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
    public string CavitiesText => Cavities?.ToString() ?? string.Empty;
    public string PackagingQuantityText => PackagingQuantity?.ToString("N0") ?? string.Empty;
}
