using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class ProAlphaViewModel : INotifyPropertyChanged, IDisposable
{
    private const string SettingsKey = "ProAlpha.Settings.R00123";
    private const string MappingsKey = "ProAlpha.Mappings.R00123";
    private const string PasswordKey = "ProAlpha.Secret.Password";
    private const string BearerKey = "ProAlpha.Secret.Bearer";
    private const string ApiKeyKey = "ProAlpha.Secret.ApiKey";
    private const string OAuthSecretKey = "ProAlpha.Secret.OAuthClientSecret";
    private const string OAuthRefreshKey = "ProAlpha.Secret.OAuthRefreshToken";
    private const string CertPasswordKey = "ProAlpha.Secret.CertificatePassword";
    private const string ProxyPasswordKey = "ProAlpha.Secret.ProxyPassword";

    private readonly MainViewModel _main;
    private readonly DatabaseService _database = new();
    private readonly ProtectedSettingsService _protectedSettings;
    private readonly ProAlphaIntegrationService _integration = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private ProAlphaConnectionSettings _settings = new();
    private ProAlphaOrderRecord? _selectedOrder;
    private string _statusText = "proALPHA-Schnittstelle noch nicht geladen.";
    private DateTime? _lastImport;
    private bool _isLoading;

    public ProAlphaViewModel(MainViewModel main)
    {
        _main = main;
        _protectedSettings = new ProtectedSettingsService(_database);
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        LoadOrdersCommand = new AsyncRelayCommand(_ => LoadOrdersAsync(true));
        ApplySelectedOrderCommand = new AsyncRelayCommand(_ => ApplySelectedOrderAsync());
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        ResetMappingsCommand = new RelayCommand(_ => ResetMappings());
        PreflightCommand = new RelayCommand(_ => RunPreflight());

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(60) };
        _pollTimer.Tick += async (_, _) => await LoadOrdersAsync(false);
    }

    public ObservableCollection<ProAlphaOrderRecord> Orders { get; } = new();
    public ObservableCollection<ProAlphaFieldMapping> Mappings { get; } = new();
    public ObservableCollection<IntegrationPreflightIssue> PreflightIssues { get; } = new();

    public IReadOnlyList<ProAlphaSourceMode> SourceModes { get; } = Enum.GetValues<ProAlphaSourceMode>();
    public IReadOnlyList<ProAlphaAuthenticationType> AuthenticationTypes { get; } = Enum.GetValues<ProAlphaAuthenticationType>();
    public IReadOnlyList<ProAlphaProxyMode> ProxyModes { get; } = Enum.GetValues<ProAlphaProxyMode>();
    public IReadOnlyList<string> HttpMethods { get; } = new[] { "GET", "POST" };

    public ICommand SaveSettingsCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand LoadOrdersCommand { get; }
    public ICommand ApplySelectedOrderCommand { get; }
    public ICommand BrowseFileCommand { get; }
    public ICommand ResetMappingsCommand { get; }
    public ICommand PreflightCommand { get; }

    public ProAlphaConnectionSettings Settings
    {
        get => _settings;
        private set { _settings = value; OnPropertyChanged(); }
    }

    public ProAlphaOrderRecord? SelectedOrder
    {
        get => _selectedOrder;
        set => SetField(ref _selectedOrder, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string LastImportText => _lastImport.HasValue
        ? $"Letzter Abruf: {_lastImport.Value:dd.MM.yyyy HH:mm:ss}"
        : "Noch kein erfolgreicher Abruf";

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        ConfigurePollingTimer();
        RunPreflight();
        StatusText = Settings.SourceMode == ProAlphaSourceMode.FileExport
            ? "proALPHA Datei-/Hotfolder-Import bereit. Vor dem ersten Abruf 'Vollständigkeit prüfen' ausführen."
            : "proALPHA REST-Import bereit. Vor dem ersten Abruf Endpunkt, Authentifizierung, Firmenkontext und Mapping vollständig prüfen.";
    }

    private async Task LoadSettingsAsync()
    {
        var json = await _database.GetSettingAsync(SettingsKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { Settings = JsonSerializer.Deserialize<ProAlphaConnectionSettings>(json, _jsonOptions) ?? new ProAlphaConnectionSettings(); }
            catch { Settings = new ProAlphaConnectionSettings(); }
        }

        Settings.Password = await _protectedSettings.GetSecretAsync(PasswordKey);
        Settings.BearerToken = await _protectedSettings.GetSecretAsync(BearerKey);
        Settings.ApiKeyValue = await _protectedSettings.GetSecretAsync(ApiKeyKey);
        Settings.OAuthClientSecret = await _protectedSettings.GetSecretAsync(OAuthSecretKey);
        Settings.OAuthRefreshToken = await _protectedSettings.GetSecretAsync(OAuthRefreshKey);
        Settings.ClientCertificatePassword = await _protectedSettings.GetSecretAsync(CertPasswordKey);
        Settings.ProxyPassword = await _protectedSettings.GetSecretAsync(ProxyPasswordKey);

        var mappingJson = await _database.GetSettingAsync(MappingsKey);
        if (!string.IsNullOrWhiteSpace(mappingJson))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<List<StoredMapping>>(mappingJson, _jsonOptions);
                if (stored is not null && stored.Count > 0)
                {
                    Mappings.Clear();
                    foreach (var mapping in CreateDefaultMappings())
                    {
                        var match = stored.FirstOrDefault(x => x.TargetField.Equals(mapping.TargetField, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) mapping.SourceField = match.SourceField ?? string.Empty;
                        Mappings.Add(mapping);
                    }
                    return;
                }
            }
            catch { }
        }
        ResetMappings();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            Settings.PollIntervalSeconds = Math.Clamp(Settings.PollIntervalSeconds, 10, 3600);
            Settings.HeaderRow = Math.Max(1, Settings.HeaderRow);
            Settings.TimeoutSeconds = Math.Clamp(Settings.TimeoutSeconds, 2, 300);
            Settings.RetryCount = Math.Clamp(Settings.RetryCount, 0, 10);
            Settings.RetryDelayMilliseconds = Math.Clamp(Settings.RetryDelayMilliseconds, 50, 10000);
            Settings.MaxPages = Math.Clamp(Settings.MaxPages, 1, 500);

            await _database.SetSettingAsync(SettingsKey, JsonSerializer.Serialize(CloneWithoutSecrets(Settings), _jsonOptions));
            await _database.SetSettingAsync(MappingsKey, JsonSerializer.Serialize(Mappings.Select(m => new StoredMapping(m.TargetField, m.SourceField)).ToList(), _jsonOptions));
            await _protectedSettings.SetSecretAsync(PasswordKey, Settings.Password);
            await _protectedSettings.SetSecretAsync(BearerKey, Settings.BearerToken);
            await _protectedSettings.SetSecretAsync(ApiKeyKey, Settings.ApiKeyValue);
            await _protectedSettings.SetSecretAsync(OAuthSecretKey, Settings.OAuthClientSecret);
            await _protectedSettings.SetSecretAsync(OAuthRefreshKey, Settings.OAuthRefreshToken);
            await _protectedSettings.SetSecretAsync(CertPasswordKey, Settings.ClientCertificatePassword);
            await _protectedSettings.SetSecretAsync(ProxyPasswordKey, Settings.ProxyPassword);

            ConfigurePollingTimer();
            RunPreflight();
            StatusText = "proALPHA-Einstellungen gespeichert. Kennwörter, Tokens und Client-Secret wurden mit Windows-DPAPI geschützt.";
        }
        catch (Exception ex)
        {
            StatusText = $"proALPHA-Einstellungen konnten nicht gespeichert werden: {ex.Message}";
        }
    }

    private void RunPreflight()
    {
        var result = _integration.ValidateConfiguration(Settings, Mappings);
        PreflightIssues.Clear();
        foreach (var issue in result.Issues) PreflightIssues.Add(issue);
        StatusText = result.Summary + (result.Issues.Count == 0 ? string.Empty : " Hinweise siehe Prüfliste.");
    }

    private async Task TestConnectionAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            RunPreflight();
            if (PreflightIssues.Any(i => i.IsError)) return;
            StatusText = "proALPHA-Verbindung/Quelle wird geprüft …";
            StatusText = await _integration.TestConnectionAsync(Settings, Mappings);
        }
        catch (Exception ex) { StatusText = $"proALPHA-Test fehlgeschlagen: {ex.Message}"; }
        finally { _isLoading = false; }
    }

    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading || !Settings.Enabled) return;
        if (!userInitiated && !await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ProAlpha)) return;
        _isLoading = true;
        try
        {
            if (userInitiated) StatusText = "proALPHA-Auftragsdaten werden gelesen …";
            var orders = await _integration.LoadOrdersAsync(Settings, Mappings);
            Orders.Clear();
            foreach (var order in orders) Orders.Add(order);
            SelectedOrder = Orders.FirstOrDefault();
            _lastImport = DateTime.Now;
            OnPropertyChanged(nameof(LastImportText));
            StatusText = $"proALPHA-Abruf erfolgreich: {Orders.Count:N0} gültige Aufträge. Es wird nichts automatisch gestartet.";
        }
        catch (Exception ex) { StatusText = $"proALPHA-Abruf fehlgeschlagen: {ex.Message}"; }
        finally { _isLoading = false; }
    }

    private async Task ApplySelectedOrderAsync()
    {
        if (!await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ProAlpha))
        {
            StatusText = "proALPHA ist nicht die führende Auftragsquelle. Unter Administration → Auftragsquellen zuerst proALPHA aktivieren.";
            return;
        }
        var order = SelectedOrder;
        if (order is null) { StatusText = "Bitte zuerst einen proALPHA-Auftrag auswählen."; return; }
        var machine = ResolveMachine(order);
        if (machine is null)
        {
            StatusText = $"Keine Partcounter-Maschine für proALPHA-Auftrag {order.OrderNumber} gefunden. WorkCenter/Maschinen-ID mappen oder Alias hinterlegen.";
            return;
        }

        var article = _main.Articles.FirstOrDefault(a => a.ArticleNumber.Equals(order.ArticleNumber, StringComparison.OrdinalIgnoreCase));
        if (article is null)
        {
            if (!Settings.CreateMissingArticles) { StatusText = $"Artikel {order.ArticleNumber} fehlt im Partcounter-Artikelstamm."; return; }
            if (order.Cavities is null or < 1 or > 64 || order.PackagingQuantity is null or 0)
            {
                StatusText = $"Artikel {order.ArticleNumber} kann nicht automatisch angelegt werden. Dafür müssen Kavitäten (1–64) und VE-Menge aus proALPHA geliefert/gemappt werden.";
                return;
            }
            article = new ArticleDefinition(0, order.ArticleNumber,
                string.IsNullOrWhiteSpace(order.ArticleDescription) ? $"proALPHA Artikel {order.ArticleNumber}" : order.ArticleDescription,
                order.ToolNumber, order.Cavities.Value, order.PackagingQuantity.Value, true);
            await _database.UpsertArticleAsync(article);
            _main.Articles.Add(article);
        }
        else if (Settings.UpdateExistingArticles)
        {
            var updated = article with
            {
                Description = string.IsNullOrWhiteSpace(order.ArticleDescription) ? article.Description : order.ArticleDescription,
                ToolNumber = string.IsNullOrWhiteSpace(order.ToolNumber) ? article.ToolNumber : order.ToolNumber,
                ActiveCavities = order.Cavities is > 0 and <= 64 ? order.Cavities.Value : article.ActiveCavities,
                PackagingQuantity = order.PackagingQuantity is > 0 ? order.PackagingQuantity.Value : article.PackagingQuantity
            };
            await _database.UpsertArticleAsync(updated);
            var index = _main.Articles.IndexOf(article);
            if (index >= 0) _main.Articles[index] = updated;
            article = updated;
        }

        _main.SelectedMachine = machine;
        _main.SelectedArticle = article;
        _main.OrderNumber = order.OrderNumber;
        _main.OrderTargetQuantity = order.OrderQuantity;
        StatusText = $"proALPHA-Auftrag {order.OrderNumber} übernommen: {machine.DisplayName}, Artikel {article.ArticleNumber}, Soll {order.OrderQuantity:N0}.";
        if (Settings.AutoStartOnApply) _main.ApplyArticleCommand.Execute(null);
    }

    private MachineState? ResolveMachine(ProAlphaOrderRecord order)
    {
        if (order.MachineNumber.HasValue)
        {
            var direct = _main.Machines.FirstOrDefault(m => m.Configuration.MachineNumber == order.MachineNumber.Value);
            if (direct is not null) return direct;
        }
        var aliases = ParseAliases(Settings.MachineAliasMap);
        foreach (var key in new[] { order.WorkCenter, order.MachineExternalId, order.MachineName })
        {
            if (!string.IsNullOrWhiteSpace(key) && aliases.TryGetValue(key.Trim(), out var number))
            {
                var machine = _main.Machines.FirstOrDefault(m => m.Configuration.MachineNumber == number);
                if (machine is not null) return machine;
            }
        }
        return !string.IsNullOrWhiteSpace(order.MachineName)
            ? _main.Machines.FirstOrDefault(m => m.Configuration.Name.Equals(order.MachineName.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static Dictionary<string, int> ParseAliases(string? text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var raw in text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator < 1) continue;
            var alias = line[..separator].Trim();
            var numberText = line[(separator + 1)..].Trim().TrimStart('M', 'm');
            if (int.TryParse(numberText, out var number) && number is >= 1 and <= 30) result[alias] = number;
        }
        return result;
    }

    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "proALPHA-Auftragsdatei auswählen",
            Filter = "proALPHA Auftragsdaten (*.xlsx;*.xlsm;*.csv;*.txt;*.tsv)|*.xlsx;*.xlsm;*.csv;*.txt;*.tsv|Alle Dateien (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            Settings.FilePath = dialog.FileName;
            OnPropertyChanged(nameof(Settings));
            StatusText = $"proALPHA-Quelldatei gewählt: {dialog.FileName}";
        }
    }

    private void ConfigurePollingTimer()
    {
        _pollTimer.Stop();
        _pollTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.PollIntervalSeconds, 10, 3600));
        if (Settings.Enabled && Settings.AutoPoll) _pollTimer.Start();
    }

    private void ResetMappings()
    {
        Mappings.Clear();
        foreach (var mapping in CreateDefaultMappings()) Mappings.Add(mapping);
        StatusText = "proALPHA-Feldmapping auf neutrale Standardnamen zurückgesetzt. Quellspalten/JSON-Pfade an die konkrete API bzw. Exportdatei anpassen.";
    }

    private static IReadOnlyList<ProAlphaFieldMapping> CreateDefaultMappings() => new[]
    {
        new ProAlphaFieldMapping("MachineNumber", "MachineNumber", false, "Partcounter-Maschinennummer 1–30", "7"),
        new ProAlphaFieldMapping("MachineName", "MachineName", false, "Maschinenname", "Spritzgussmaschine 07"),
        new ProAlphaFieldMapping("MachineExternalId", "MachineId", false, "Externe Maschinen-ID", "ARB-0470-07"),
        new ProAlphaFieldMapping("WorkCenter", "WorkCenter", false, "proALPHA Arbeitsplatz/Ressource; typischer Schlüssel zur Maschinenzuordnung", "SGM-07"),
        new ProAlphaFieldMapping("OrderNumber", "OrderNumber", true, "Fertigungs-/Produktionsauftrag", "FA-2026-4711"),
        new ProAlphaFieldMapping("OperationNumber", "OperationNumber", false, "Arbeitsgang/Vorgang", "0010"),
        new ProAlphaFieldMapping("ArticleNumber", "ArticleNumber", true, "Artikel-/Teilenummer", "47110815"),
        new ProAlphaFieldMapping("OrderQuantity", "OrderQuantity", true, "Auftrags-Sollmenge", "200000"),
        new ProAlphaFieldMapping("ArticleDescription", "ArticleDescription", false, "Artikelbezeichnung", "Gehäuse schwarz"),
        new ProAlphaFieldMapping("ToolNumber", "ToolNumber", false, "Werkzeugnummer, falls in ERP geführt", "WZ-1842"),
        new ProAlphaFieldMapping("Cavities", "Cavities", false, "Aktive Kavitäten, falls in ERP geführt", "8"),
        new ProAlphaFieldMapping("PackagingQuantity", "PackagingQuantity", false, "VE-Menge, falls in ERP geführt", "8000"),
        new ProAlphaFieldMapping("PlannedStart", "PlannedStart", false, "Geplanter Start", "29.08.2026 22:00"),
        new ProAlphaFieldMapping("PlannedEnd", "PlannedEnd", false, "Geplantes Ende", "30.08.2026 06:00"),
        new ProAlphaFieldMapping("OrderStatus", "OrderStatus", false, "Freigabe-/Auftragsstatus", "Released"),
        new ProAlphaFieldMapping("Priority", "Priority", false, "Priorität", "10"),
        new ProAlphaFieldMapping("MaterialNumber", "MaterialNumber", false, "Materialnummer", "PBT-GF20-001"),
        new ProAlphaFieldMapping("MaterialDescription", "MaterialDescription", false, "Materialbezeichnung", "PBT GF20 schwarz"),
        new ProAlphaFieldMapping("Batch", "Batch", false, "Charge", "CH-260829"),
        new ProAlphaFieldMapping("Color", "Color", false, "Farbe", "schwarz"),
        new ProAlphaFieldMapping("CustomerOrder", "CustomerOrder", false, "Kundenauftrag", "KA-1004711"),
        new ProAlphaFieldMapping("CompanyCode", "Company", false, "Firma/Mandant", "100"),
        new ProAlphaFieldMapping("PlantCode", "Plant", false, "Werk/Standort", "01"),
        new ProAlphaFieldMapping("LastChanged", "LastChanged", false, "Letzte Änderung", "2026-08-29T21:00:00Z")
    };

    private static ProAlphaConnectionSettings CloneWithoutSecrets(ProAlphaConnectionSettings source)
    {
        var clone = JsonSerializer.Deserialize<ProAlphaConnectionSettings>(JsonSerializer.Serialize(source)) ?? new ProAlphaConnectionSettings();
        clone.Password = string.Empty;
        clone.BearerToken = string.Empty;
        clone.ApiKeyValue = string.Empty;
        clone.OAuthClientSecret = string.Empty;
        clone.OAuthRefreshToken = string.Empty;
        clone.ClientCertificatePassword = string.Empty;
        clone.ProxyPassword = string.Empty;
        return clone;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.IsEnabled = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private sealed record StoredMapping(string TargetField, string SourceField);
    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private sealed class AsyncRelayCommand(Func<object?, Task> execute) : ICommand
    {
        private bool _running;
        public bool CanExecute(object? parameter) => !_running;
        public async void Execute(object? parameter)
        {
            if (_running) return;
            _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await execute(parameter); }
            finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
        public event EventHandler? CanExecuteChanged;
    }
}
