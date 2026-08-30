using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class AlsViewModel : INotifyPropertyChanged, IDisposable
{
    private const string SettingsKey = "ALS.Settings.R0016";
    private const string MappingsKey = "ALS.Mappings.R0016";
    private const string PasswordKey = "ALS.Secret.Password";
    private const string BearerKey = "ALS.Secret.Bearer";
    private const string ApiKeyValueKey = "ALS.Secret.ApiKey";
    private const string CertificatePasswordKey = "ALS.Secret.CertificatePassword";
    private const string OAuthClientSecretKey = "ALS.Secret.OAuthClientSecret";
    private const string ProxyPasswordKey = "ALS.Secret.ProxyPassword";
    private static readonly SemaphoreSlim ProxyGate = new(1, 1);

    private readonly MainViewModel _main;
    private readonly DatabaseService _database = new();
    private readonly ProtectedSettingsService _protectedSettings;
    private readonly AlsIntegrationService _integration = new();
    private readonly AlsExtendedAccessService _extendedAccess = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private AlsConnectionSettings _settings = new();
    private AlsOrderRecord? _selectedOrder;
    private string _statusText = "ALS-Schnittstelle noch nicht geladen.";
    private DateTime? _lastImport;
    private bool _isLoading;

    public AlsViewModel(MainViewModel main)
    {
        _main = main;
        _protectedSettings = new ProtectedSettingsService(_database);

        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        LoadOrdersCommand = new AsyncRelayCommand(_ => LoadOrdersAsync(userInitiated: true));
        ApplySelectedOrderCommand = new AsyncRelayCommand(_ => ApplySelectedOrderAsync());
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        ResetMappingsCommand = new RelayCommand(_ => ResetMappings());
        PreflightCommand = new RelayCommand(_ => RunPreflight());

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _pollTimer.Tick += async (_, _) => await LoadOrdersAsync(userInitiated: false);
    }

    public ObservableCollection<AlsOrderRecord> Orders { get; } = new();
    public ObservableCollection<AlsFieldMapping> Mappings { get; } = new();
    public ObservableCollection<IntegrationPreflightIssue> PreflightIssues { get; } = new();

    public IReadOnlyList<AlsSourceMode> SourceModes { get; } = Enum.GetValues<AlsSourceMode>();
    public IReadOnlyList<AlsAuthenticationType> AuthenticationTypes { get; } = Enum.GetValues<AlsAuthenticationType>();
    public IReadOnlyList<AlsProxyMode> ProxyModes { get; } = Enum.GetValues<AlsProxyMode>();
    public IReadOnlyList<string> HttpMethods { get; } = new[] { "GET", "POST" };

    public ICommand SaveSettingsCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand LoadOrdersCommand { get; }
    public ICommand ApplySelectedOrderCommand { get; }
    public ICommand BrowseFileCommand { get; }
    public ICommand ResetMappingsCommand { get; }
    public ICommand PreflightCommand { get; }

    public AlsConnectionSettings Settings
    {
        get => _settings;
        private set
        {
            _settings = value;
            OnPropertyChanged();
        }
    }

    public AlsOrderRecord? SelectedOrder
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
        RunPreflight(updateStatus: false);
        StatusText = Settings.SourceMode == AlsSourceMode.FileExport
            ? "ALS-Dateiimport bereit. Quellpfad und Feldmapping prüfen, dann 'Aufträge laden'."
            : "ALS-REST-Import bereit. URL, Authentifizierung und Feldmapping prüfen, dann Verbindung testen.";
    }

    private async Task LoadSettingsAsync()
    {
        var settingsJson = await _database.GetSettingAsync(SettingsKey);
        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                Settings = JsonSerializer.Deserialize<AlsConnectionSettings>(settingsJson, _jsonOptions) ?? new AlsConnectionSettings();
            }
            catch
            {
                Settings = new AlsConnectionSettings();
            }
        }

        Settings.Password = await _protectedSettings.GetSecretAsync(PasswordKey);
        Settings.BearerToken = await _protectedSettings.GetSecretAsync(BearerKey);
        Settings.ApiKeyValue = await _protectedSettings.GetSecretAsync(ApiKeyValueKey);
        Settings.ClientCertificatePassword = await _protectedSettings.GetSecretAsync(CertificatePasswordKey);
        Settings.OAuthClientSecret = await _protectedSettings.GetSecretAsync(OAuthClientSecretKey);
        Settings.ProxyPassword = await _protectedSettings.GetSecretAsync(ProxyPasswordKey);

        var mappingJson = await _database.GetSettingAsync(MappingsKey);
        if (!string.IsNullOrWhiteSpace(mappingJson))
        {
            try
            {
                var storedMappings = JsonSerializer.Deserialize<List<StoredMapping>>(mappingJson, _jsonOptions);
                if (storedMappings is not null && storedMappings.Count > 0)
                {
                    Mappings.Clear();
                    foreach (var defaultMapping in CreateDefaultMappings())
                    {
                        var stored = storedMappings.FirstOrDefault(m =>
                            m.TargetField.Equals(defaultMapping.TargetField, StringComparison.OrdinalIgnoreCase));
                        if (stored is not null)
                            defaultMapping.SourceField = stored.SourceField ?? string.Empty;
                        Mappings.Add(defaultMapping);
                    }
                    return;
                }
            }
            catch
            {
            }
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

            var persisted = CloneSettingsWithoutSecrets(Settings);
            await _database.SetSettingAsync(SettingsKey, JsonSerializer.Serialize(persisted, _jsonOptions));
            await _database.SetSettingAsync(
                MappingsKey,
                JsonSerializer.Serialize(Mappings.Select(m => new StoredMapping(m.TargetField, m.SourceField)).ToList(), _jsonOptions));

            await _protectedSettings.SetSecretAsync(PasswordKey, Settings.Password);
            await _protectedSettings.SetSecretAsync(BearerKey, Settings.BearerToken);
            await _protectedSettings.SetSecretAsync(ApiKeyValueKey, Settings.ApiKeyValue);
            await _protectedSettings.SetSecretAsync(CertificatePasswordKey, Settings.ClientCertificatePassword);
            await _protectedSettings.SetSecretAsync(OAuthClientSecretKey, Settings.OAuthClientSecret);
            await _protectedSettings.SetSecretAsync(ProxyPasswordKey, Settings.ProxyPassword);

            ConfigurePollingTimer();
            RunPreflight(updateStatus: false);
            StatusText = "ALS-Einstellungen gespeichert. Kennwörter, Tokens und Client-Secrets wurden mit Windows-DPAPI für den aktuellen Benutzer geschützt.";
        }
        catch (Exception ex)
        {
            StatusText = $"ALS-Einstellungen konnten nicht gespeichert werden: {ex.Message}";
        }
    }

    private void RunPreflight(bool updateStatus = true)
    {
        var result = _extendedAccess.Validate(Settings, Mappings);
        PreflightIssues.Clear();
        foreach (var issue in result.Issues)
            PreflightIssues.Add(issue);
        if (updateStatus)
            StatusText = result.Summary + (result.Issues.Count > 0 ? " Hinweise siehe erweiterten ALS-Zugangscheck." : string.Empty);
    }

    private async Task TestConnectionAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            RunPreflight(updateStatus: false);
            if (PreflightIssues.Any(issue => issue.IsError))
            {
                StatusText = $"ALS-Verbindungscheck abgebrochen: {PreflightIssues.Count(i => i.IsError)} Pflichtangabe(n) fehlen.";
                return;
            }

            StatusText = "ALS-Verbindung/Quelle wird geprüft …";
            StatusText = await ExecuteIntegrationWithAccessAsync(
                effective => _integration.TestConnectionAsync(effective, Mappings));
        }
        catch (Exception ex)
        {
            StatusText = $"ALS-Test fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading) return;
        if (!userInitiated && !await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ArburgAls)) return;
        _isLoading = true;
        try
        {
            RunPreflight(updateStatus: false);
            if (PreflightIssues.Any(issue => issue.IsError))
            {
                if (userInitiated)
                    StatusText = $"ALS-Abruf nicht gestartet: {PreflightIssues.Count(i => i.IsError)} Pflichtangabe(n) fehlen.";
                return;
            }

            if (userInitiated)
                StatusText = "ALS-Auftragsdaten werden gelesen …";

            var orders = await ExecuteIntegrationWithAccessAsync(
                effective => _integration.LoadOrdersAsync(effective, Mappings));
            Orders.Clear();
            foreach (var order in orders)
                Orders.Add(order);

            SelectedOrder = Orders.FirstOrDefault();
            _lastImport = DateTime.Now;
            OnPropertyChanged(nameof(LastImportText));
            StatusText = $"ALS-Abruf erfolgreich: {Orders.Count:N0} gültige Aufträge verfügbar. Es wird nichts automatisch gestartet.";
        }
        catch (Exception ex)
        {
            StatusText = $"ALS-Abruf fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<T> ExecuteIntegrationWithAccessAsync<T>(Func<AlsConnectionSettings, Task<T>> operation)
    {
        var effective = await BuildEffectiveSettingsAsync();
        await ProxyGate.WaitAsync();
        var previousProxy = HttpClient.DefaultProxy;
        try
        {
            if (Settings.ProxyMode == AlsProxyMode.Custom)
            {
                var proxy = new WebProxy(Settings.ProxyUrl);
                if (!string.IsNullOrWhiteSpace(Settings.ProxyUsername))
                    proxy.Credentials = new NetworkCredential(Settings.ProxyUsername, Settings.ProxyPassword);
                HttpClient.DefaultProxy = proxy;
            }
            else if (Settings.ProxyMode == AlsProxyMode.None)
            {
                HttpClient.DefaultProxy = new NoProxy();
            }

            return await operation(effective);
        }
        finally
        {
            HttpClient.DefaultProxy = previousProxy;
            ProxyGate.Release();
        }
    }

    private async Task<AlsConnectionSettings> BuildEffectiveSettingsAsync()
    {
        if (Settings.AuthenticationType != AlsAuthenticationType.OAuth2ClientCredentials)
            return Settings;

        var token = await _extendedAccess.AcquireOAuthClientCredentialsTokenAsync(Settings);
        var clone = JsonSerializer.Deserialize<AlsConnectionSettings>(JsonSerializer.Serialize(Settings))
            ?? throw new InvalidOperationException("ALS-Einstellungen konnten für OAuth2 nicht vorbereitet werden.");
        clone.AuthenticationType = AlsAuthenticationType.Bearer;
        clone.BearerToken = token;
        return clone;
    }

    private async Task ApplySelectedOrderAsync()
    {
        if (!await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ArburgAls))
        {
            StatusText = "ARBURG ALS ist nicht die führende Auftragsquelle. Unter Administration → Auftragsquellen zuerst ALS aktivieren.";
            return;
        }
        var order = SelectedOrder;
        if (order is null)
        {
            StatusText = "Bitte zuerst einen ALS-Auftrag auswählen.";
            return;
        }

        var machine = ResolveMachine(order);
        if (machine is null)
        {
            StatusText =
                $"Keine Partcounter-Maschine für ALS-Auftrag {order.OrderNumber} gefunden. " +
                "MachineNumber/MachineName prüfen oder eine Zuordnung unter 'Maschinen-Alias' eintragen.";
            return;
        }

        if (order.OrderQuantity == 0)
        {
            StatusText = $"ALS-Auftrag {order.OrderNumber} hat keine gültige Auftragsmenge.";
            return;
        }

        var article = _main.Articles.FirstOrDefault(a =>
            a.ArticleNumber.Equals(order.ArticleNumber, StringComparison.OrdinalIgnoreCase));

        if (article is null)
        {
            if (!Settings.CreateMissingArticles)
            {
                StatusText =
                    $"Artikel {order.ArticleNumber} ist in Partcounter nicht vorhanden. " +
                    "Entweder Artikelstamm anlegen oder 'Fehlende Artikel aus ALS anlegen' aktivieren.";
                return;
            }

            if (order.Cavities is null or < 1 or > 64 || order.PackagingQuantity is null or 0)
            {
                StatusText =
                    $"Artikel {order.ArticleNumber} kann nicht automatisch angelegt werden: " +
                    "ALS-Datensatz benötigt gültige Kavitäten (1–64) und VE-Menge.";
                return;
            }

            article = new ArticleDefinition(
                0,
                order.ArticleNumber,
                string.IsNullOrWhiteSpace(order.ArticleDescription) ? $"ALS Artikel {order.ArticleNumber}" : order.ArticleDescription,
                order.ToolNumber ?? string.Empty,
                order.Cavities.Value,
                order.PackagingQuantity.Value,
                true);

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

        StatusText =
            $"ALS-Auftrag {order.OrderNumber} wurde in die Partcounter-Auftragsmaske übernommen: " +
            $"{machine.DisplayName}, Artikel {article.ArticleNumber}, Soll {order.OrderQuantity:N0}.";

        if (Settings.AutoStartOnApply)
        {
            StatusText += " Automatischer Start ist aktiviert; Partcounter startet den Auftrag jetzt.";
            _main.ApplyArticleCommand.Execute(null);
        }
    }

    private MachineState? ResolveMachine(AlsOrderRecord order)
    {
        if (order.MachineNumber.HasValue)
        {
            var direct = _main.Machines.FirstOrDefault(m => m.Configuration.MachineNumber == order.MachineNumber.Value);
            if (direct is not null) return direct;
        }

        var aliases = ParseMachineAliases(Settings.MachineAliasMap);
        foreach (var key in new[] { order.MachineExternalId, order.MachineName })
        {
            if (!string.IsNullOrWhiteSpace(key) && aliases.TryGetValue(key.Trim(), out var machineNumber))
            {
                var aliased = _main.Machines.FirstOrDefault(m => m.Configuration.MachineNumber == machineNumber);
                if (aliased is not null) return aliased;
            }
        }

        if (!string.IsNullOrWhiteSpace(order.MachineName))
        {
            return _main.Machines.FirstOrDefault(m =>
                m.Configuration.Name.Equals(order.MachineName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static Dictionary<string, int> ParseMachineAliases(string? text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator < 1) continue;

            var alias = line[..separator].Trim();
            var machineText = line[(separator + 1)..].Trim().TrimStart('M', 'm');
            if (int.TryParse(machineText, out var machineNumber) && machineNumber is >= 1 and <= 30)
                result[alias] = machineNumber;
        }
        return result;
    }

    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "ALS-Auftragsdatei auswählen",
            Filter = "ALS Auftragsdaten (*.xlsx;*.xlsm;*.csv;*.txt;*.tsv)|*.xlsx;*.xlsm;*.csv;*.txt;*.tsv|Alle Dateien (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.FilePath = dialog.FileName;
            OnPropertyChanged(nameof(Settings));
            StatusText = $"ALS-Quelldatei gewählt: {dialog.FileName}";
        }
    }

    private void ResetMappings()
    {
        Mappings.Clear();
        foreach (var mapping in CreateDefaultMappings())
            Mappings.Add(mapping);
        StatusText = "ALS-Feldmapping auf Partcounter-Standardnamen zurückgesetzt. Quellspalte/JSON-Pfad an Ihre ALS-Ausgabe anpassen.";
    }

    private static IReadOnlyList<AlsFieldMapping> CreateDefaultMappings() => new[]
    {
        new AlsFieldMapping("MachineNumber", "MachineNumber", false, "Partcounter-Maschinennummer 1–30", "7"),
        new AlsFieldMapping("MachineName", "MachineName", false, "Maschinenname aus ALS", "ALLROUNDER 470 A"),
        new AlsFieldMapping("MachineExternalId", "MachineId", false, "ALS-interne Maschinen-ID; über Alias-Tabelle zuordenbar", "ARB-0470-07"),
        new AlsFieldMapping("OrderNumber", "OrderNumber", true, "Produktions-/Fertigungsauftragsnummer", "FA-2026-4711"),
        new AlsFieldMapping("ArticleNumber", "ArticleNumber", true, "Artikelnummer", "47110815"),
        new AlsFieldMapping("OrderQuantity", "OrderQuantity", true, "Gesamt-Sollstückzahl des Auftrags", "200000"),
        new AlsFieldMapping("ArticleDescription", "ArticleDescription", false, "Artikelbezeichnung", "Gehäuse schwarz"),
        new AlsFieldMapping("ToolNumber", "ToolNumber", false, "Werkzeugnummer", "WZ-1842"),
        new AlsFieldMapping("Cavities", "Cavities", false, "Aktive Kavitäten / Teile pro Zyklus", "8"),
        new AlsFieldMapping("PackagingQuantity", "PackagingQuantity", false, "Standard-Stückzahl pro VE", "8000"),
        new AlsFieldMapping("PlannedStart", "PlannedStart", false, "Geplanter Produktionsstart", "25.08.2026 22:00"),
        new AlsFieldMapping("PlannedEnd", "PlannedEnd", false, "Geplantes Produktionsende", "26.08.2026 06:00"),
        new AlsFieldMapping("OrderStatus", "OrderStatus", false, "ALS-Auftragsstatus/Freigabestatus", "Released"),
        new AlsFieldMapping("OperationNumber", "OperationNumber", false, "Arbeitsgang/Vorgangsnummer", "0010"),
        new AlsFieldMapping("Priority", "Priority", false, "Priorität/Reihenfolge", "10"),
        new AlsFieldMapping("MaterialNumber", "MaterialNumber", false, "Materialnummer", "MAT-PA6-GF30"),
        new AlsFieldMapping("MaterialDescription", "MaterialDescription", false, "Materialbezeichnung", "PA6 GF30 schwarz"),
        new AlsFieldMapping("Batch", "Batch", false, "Material-/Produktionscharge", "B260825-01"),
        new AlsFieldMapping("Color", "Color", false, "Farbe/Farbkennung", "RAL 9005"),
        new AlsFieldMapping("CustomerOrder", "CustomerOrder", false, "Kundenauftrag/Referenz", "KA-55291"),
        new AlsFieldMapping("LastChanged", "LastChanged", false, "Zeitpunkt der letzten ALS-Änderung", "2026-08-25T21:10:00")
    };

    private static AlsConnectionSettings CloneSettingsWithoutSecrets(AlsConnectionSettings source)
    {
        var json = JsonSerializer.Serialize(source);
        var clone = JsonSerializer.Deserialize<AlsConnectionSettings>(json) ?? new AlsConnectionSettings();
        clone.Password = string.Empty;
        clone.BearerToken = string.Empty;
        clone.ApiKeyValue = string.Empty;
        clone.ClientCertificatePassword = string.Empty;
        clone.OAuthClientSecret = string.Empty;
        clone.ProxyPassword = string.Empty;
        return clone;
    }

    private void ConfigurePollingTimer()
    {
        _pollTimer.Stop();
        _pollTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.PollIntervalSeconds, 10, 3600));
        if (Settings.Enabled && Settings.AutoPoll)
            _pollTimer.Start();
    }

    public void Dispose()
    {
        _pollTimer.Stop();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record StoredMapping(string TargetField, string SourceField);

    private sealed class NoProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }
        public Uri? GetProxy(Uri destination) => destination;
        public bool IsBypassed(Uri host) => true;
    }

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
            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await execute(parameter); }
            finally
            {
                _running = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanExecuteChanged;
    }
}
