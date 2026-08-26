using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class LabelDesignerViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _main;
    private readonly LabelTemplateService _templates = new();
    private readonly LabelPrintService _printer = new();

    private LabelTemplateDefinition? _selectedTemplate;
    private LabelElementEditorRow? _selectedElement;
    private LabelDataToken? _selectedToken;
    private string _workingId = Guid.NewGuid().ToString("N");
    private string _templateName = "Neue Vorlage";
    private double _widthMm = 148;
    private double _heightMm = 105;
    private bool _isDefault;
    private string _assignedArticleNumber = string.Empty;
    private string _statusText = "Etiketteneditor bereit.";
    private bool _suspendPreviewEvents;

    public LabelDesignerViewModel(MainViewModel main)
    {
        _main = main;

        NewTemplateCommand = new RelayCommand(_ => NewTemplate());
        CloneTemplateCommand = new RelayCommand(_ => CloneTemplate(), _ => SelectedTemplate is not null);
        SaveTemplateCommand = new AsyncRelayCommand(_ => SaveTemplateAsync());
        DeleteTemplateCommand = new AsyncRelayCommand(_ => DeleteTemplateAsync(), _ => SelectedTemplate is not null);
        AddElementCommand = new RelayCommand(AddElement);
        DeleteElementCommand = new RelayCommand(_ => DeleteSelectedElement(), _ => SelectedElement is not null);
        InsertTokenCommand = new RelayCommand(_ => InsertSelectedToken(), _ => SelectedElement is not null && SelectedToken is not null);
        ApplySizePresetCommand = new RelayCommand(ApplySizePreset);
        TestPrintCommand = new AsyncRelayCommand(_ => TestPrintAsync());
        RefreshPreviewCommand = new RelayCommand(_ => RaiseDesignerChanged());
    }

    public ObservableCollection<LabelTemplateDefinition> Templates { get; } = new();
    public ObservableCollection<LabelElementEditorRow> Elements { get; } = new();
    public ObservableCollection<string> ArticleAssignments { get; } = new();

    public IReadOnlyList<LabelDataToken> Tokens => LabelTemplateService.AvailableTokens;
    public IReadOnlyList<LabelTextAlignment> Alignments { get; } = Enum.GetValues<LabelTextAlignment>();
    public IReadOnlyList<LabelElementType> ElementTypes { get; } = Enum.GetValues<LabelElementType>();

    public ICommand NewTemplateCommand { get; }
    public ICommand CloneTemplateCommand { get; }
    public ICommand SaveTemplateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand AddElementCommand { get; }
    public ICommand DeleteElementCommand { get; }
    public ICommand InsertTokenCommand { get; }
    public ICommand ApplySizePresetCommand { get; }
    public ICommand TestPrintCommand { get; }
    public ICommand RefreshPreviewCommand { get; }

    public event EventHandler? DesignerChanged;

    public LabelTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (ReferenceEquals(_selectedTemplate, value)) return;
            _selectedTemplate = value;
            OnPropertyChanged();
            if (value is not null)
                LoadWorkingCopy(value);
        }
    }

    public LabelElementEditorRow? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (ReferenceEquals(_selectedElement, value)) return;
            _selectedElement = value;
            OnPropertyChanged();
            RaiseDesignerChanged();
        }
    }

    public LabelDataToken? SelectedToken
    {
        get => _selectedToken;
        set { if (ReferenceEquals(_selectedToken, value)) return; _selectedToken = value; OnPropertyChanged(); }
    }

    public string TemplateName
    {
        get => _templateName;
        set { value ??= string.Empty; if (_templateName == value) return; _templateName = value; OnPropertyChanged(); }
    }

    public double WidthMm
    {
        get => _widthMm;
        set
        {
            value = Math.Round(Math.Clamp(value, 20, 500), 1);
            if (Math.Abs(_widthMm - value) < 0.001) return;
            _widthMm = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeText));
            ClampAllElements();
            RaiseDesignerChanged();
        }
    }

    public double HeightMm
    {
        get => _heightMm;
        set
        {
            value = Math.Round(Math.Clamp(value, 20, 500), 1);
            if (Math.Abs(_heightMm - value) < 0.001) return;
            _heightMm = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeText));
            ClampAllElements();
            RaiseDesignerChanged();
        }
    }

    public string SizeText => $"{WidthMm:0.#} × {HeightMm:0.#} mm";

    public bool IsDefault
    {
        get => _isDefault;
        set { if (_isDefault == value) return; _isDefault = value; OnPropertyChanged(); }
    }

    public string AssignedArticleNumber
    {
        get => _assignedArticleNumber;
        set { value ??= string.Empty; if (_assignedArticleNumber == value) return; _assignedArticleNumber = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); }
    }

    public string PrinterText => string.IsNullOrWhiteSpace(_main.LabelPrinterName)
        ? "Kein Drucker eingestellt"
        : _main.LabelPrinterName;

    public PackagingUnitRecord SampleRecord => BuildSampleRecord();

    public LabelTemplateDefinition WorkingTemplate => BuildWorkingTemplate();

    public async Task InitializeAsync()
    {
        await _templates.InitializeAsync();
        RefreshArticleAssignments();
        await ReloadTemplatesAsync();
        OnPropertyChanged(nameof(PrinterText));
    }

    public void SelectElement(LabelElementEditorRow? row) => SelectedElement = row;

    public void MoveElement(LabelElementEditorRow row, double xMm, double yMm)
    {
        row.Model.Xmm = Math.Round(Math.Clamp(xMm, 0, Math.Max(0, WidthMm - row.WidthMm)), 1);
        row.Model.Ymm = Math.Round(Math.Clamp(yMm, 0, Math.Max(0, HeightMm - row.HeightMm)), 1);
        row.NotifyPositionChanged();
        RaiseDesignerChanged();
    }

    private async Task ReloadTemplatesAsync(string? selectId = null)
    {
        Templates.Clear();
        var loaded = await _templates.LoadTemplatesAsync();
        foreach (var template in loaded)
            Templates.Add(template);

        SelectedTemplate = Templates.FirstOrDefault(t =>
                               string.Equals(t.Id, selectId, StringComparison.OrdinalIgnoreCase))
                           ?? Templates.FirstOrDefault();
        StatusText = $"{Templates.Count} Etikettenvorlage(n) geladen.";
    }

    private void RefreshArticleAssignments()
    {
        ArticleAssignments.Clear();
        ArticleAssignments.Add(string.Empty);
        foreach (var article in _main.Articles.OrderBy(a => a.ArticleNumber, StringComparer.OrdinalIgnoreCase))
            ArticleAssignments.Add(article.ArticleNumber);
    }

    private void LoadWorkingCopy(LabelTemplateDefinition template)
    {
        _suspendPreviewEvents = true;
        try
        {
            _workingId = template.Id;
            TemplateName = template.Name;
            _widthMm = template.WidthMm;
            _heightMm = template.HeightMm;
            _isDefault = template.IsDefault;
            _assignedArticleNumber = template.AssignedArticleNumber ?? string.Empty;
            OnPropertyChanged(nameof(WidthMm));
            OnPropertyChanged(nameof(HeightMm));
            OnPropertyChanged(nameof(SizeText));
            OnPropertyChanged(nameof(IsDefault));
            OnPropertyChanged(nameof(AssignedArticleNumber));

            ClearElementSubscriptions();
            Elements.Clear();
            foreach (var element in template.Elements.Select(e => e.DeepClone()))
            {
                var row = new LabelElementEditorRow(element);
                row.PropertyChanged += OnElementPropertyChanged;
                Elements.Add(row);
            }
            SelectedElement = Elements.FirstOrDefault();
        }
        finally
        {
            _suspendPreviewEvents = false;
        }
        RaiseDesignerChanged();
    }

    private void NewTemplate()
    {
        _selectedTemplate = null;
        OnPropertyChanged(nameof(SelectedTemplate));
        _workingId = Guid.NewGuid().ToString("N");
        TemplateName = "Neue Etikettenvorlage";
        _widthMm = 148;
        _heightMm = 105;
        _isDefault = Templates.All(t => !t.IsDefault);
        _assignedArticleNumber = string.Empty;
        OnPropertyChanged(nameof(WidthMm));
        OnPropertyChanged(nameof(HeightMm));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(IsDefault));
        OnPropertyChanged(nameof(AssignedArticleNumber));
        ClearElementSubscriptions();
        Elements.Clear();
        AddElementByType(LabelElementType.Text, false);
        StatusText = "Neue Vorlage angelegt. Änderungen erst mit 'Speichern' dauerhaft.";
        RaiseDesignerChanged();
    }

    private void CloneTemplate()
    {
        var clone = BuildWorkingTemplate().DeepClone($"{TemplateName} – Kopie");
        _selectedTemplate = null;
        OnPropertyChanged(nameof(SelectedTemplate));
        LoadWorkingCopy(clone);
        _workingId = clone.Id;
        IsDefault = false;
        StatusText = "Vorlage kopiert. Bitte Namen/Zuordnung prüfen und speichern.";
    }

    private async Task SaveTemplateAsync()
    {
        try
        {
            var template = BuildWorkingTemplate();
            await _templates.SaveTemplateAsync(template);
            await ReloadTemplatesAsync(template.Id);
            StatusText = $"Vorlage '{template.Name}' gespeichert.";
        }
        catch (Exception ex)
        {
            StatusText = $"Vorlage konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate is null) return;
        try
        {
            var name = SelectedTemplate.Name;
            await _templates.DeleteTemplateAsync(SelectedTemplate.Id);
            await ReloadTemplatesAsync();
            StatusText = $"Vorlage '{name}' gelöscht.";
        }
        catch (Exception ex)
        {
            StatusText = $"Vorlage konnte nicht gelöscht werden: {ex.Message}";
        }
    }

    private void AddElement(object? parameter)
    {
        if (parameter is LabelElementType type)
            AddElementByType(type, true);
        else if (parameter is string text && Enum.TryParse<LabelElementType>(text, true, out var parsed))
            AddElementByType(parsed, true);
    }

    private void AddElementByType(LabelElementType type, bool updateStatus)
    {
        var z = Elements.Count == 0 ? 0 : Elements.Max(e => e.ZIndex) + 1;
        var model = type switch
        {
            LabelElementType.Text => new LabelElementDefinition
            {
                Type = type, Xmm = 8, Ymm = 8, WidthMm = 55, HeightMm = 9,
                Content = "Neuer Text", FontSizePt = 12, ZIndex = z
            },
            LabelElementType.DataText => new LabelElementDefinition
            {
                Type = type, Xmm = 8, Ymm = 18, WidthMm = 70, HeightMm = 9,
                Content = "Artikel: {{ArticleNumber}}", FontSizePt = 12, ZIndex = z
            },
            LabelElementType.QrCode => new LabelElementDefinition
            {
                Type = type, Xmm = 100, Ymm = 8, WidthMm = 35, HeightMm = 35,
                Content = "{{QrPayload}}", ZIndex = z
            },
            LabelElementType.Code128 => new LabelElementDefinition
            {
                Type = type, Xmm = 8, Ymm = 82, WidthMm = 125, HeightMm = 16,
                Content = "{{VE_ID}}", ZIndex = z
            },
            LabelElementType.Rectangle => new LabelElementDefinition
            {
                Type = type, Xmm = 5, Ymm = 5, WidthMm = 60, HeightMm = 25,
                BorderThickness = 1, ZIndex = z
            },
            LabelElementType.Line => new LabelElementDefinition
            {
                Type = type, Xmm = 8, Ymm = 45, WidthMm = 80, HeightMm = 0.5,
                BorderThickness = 1, ZIndex = z
            },
            _ => new LabelElementDefinition { Type = type, Xmm = 8, Ymm = 8, ZIndex = z }
        };

        model.WidthMm = Math.Min(model.WidthMm, Math.Max(1, WidthMm - model.Xmm));
        model.HeightMm = Math.Min(model.HeightMm, Math.Max(0.5, HeightMm - model.Ymm));

        var row = new LabelElementEditorRow(model);
        row.PropertyChanged += OnElementPropertyChanged;
        Elements.Add(row);
        SelectedElement = row;
        if (updateStatus)
            StatusText = $"Element '{type}' hinzugefügt.";
        RaiseDesignerChanged();
    }

    private void DeleteSelectedElement()
    {
        if (SelectedElement is null) return;
        var index = Elements.IndexOf(SelectedElement);
        SelectedElement.PropertyChanged -= OnElementPropertyChanged;
        Elements.Remove(SelectedElement);
        SelectedElement = Elements.Count == 0 ? null : Elements[Math.Clamp(index, 0, Elements.Count - 1)];
        StatusText = "Element gelöscht.";
        RaiseDesignerChanged();
    }

    private void InsertSelectedToken()
    {
        if (SelectedElement is null || SelectedToken is null) return;
        SelectedElement.Content = string.Concat(SelectedElement.Content, SelectedToken.Token);
        StatusText = $"Platzhalter {SelectedToken.Token} eingefügt.";
    }

    private void ApplySizePreset(object? parameter)
    {
        var preset = parameter?.ToString()?.Trim().ToUpperInvariant();
        switch (preset)
        {
            case "A5Q": WidthMm = 210; HeightMm = 148; break;
            case "A6Q": WidthMm = 148; HeightMm = 105; break;
            case "100X50": WidthMm = 100; HeightMm = 50; break;
            case "100X100": WidthMm = 100; HeightMm = 100; break;
            case "150X100": WidthMm = 150; HeightMm = 100; break;
            default: return;
        }
        StatusText = $"Etikettenformat auf {SizeText} gesetzt.";
    }

    private async Task TestPrintAsync()
    {
        if (string.IsNullOrWhiteSpace(_main.LabelPrinterName))
        {
            StatusText = "Kein Etikettendrucker konfiguriert. Drucker unter den Druckeinstellungen festlegen.";
            return;
        }

        try
        {
            var ok = await _printer.PrintTemplateAsync(BuildSampleRecord(), BuildWorkingTemplate(), _main.LabelPrinterName);
            StatusText = ok
                ? $"Testetikett mit aktueller Editorvorlage an '{_main.LabelPrinterName}' gesendet."
                : "Testdruck fehlgeschlagen. Drucker/Windows-Druckwarteschlange prüfen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Testdruck fehlgeschlagen: {ex.Message}";
        }
    }

    private LabelTemplateDefinition BuildWorkingTemplate()
    {
        return new LabelTemplateDefinition
        {
            Id = _workingId,
            Name = string.IsNullOrWhiteSpace(TemplateName) ? "Unbenannte Vorlage" : TemplateName.Trim(),
            WidthMm = WidthMm,
            HeightMm = HeightMm,
            IsDefault = IsDefault,
            AssignedArticleNumber = string.IsNullOrWhiteSpace(AssignedArticleNumber) ? null : AssignedArticleNumber.Trim(),
            UpdatedAtUtc = DateTime.UtcNow,
            Elements = Elements.Select(e => e.Model.DeepClone()).Select((e, index) =>
            {
                // Für die Arbeitskopie muss die Element-ID stabil bleiben.
                var source = Elements[index].Model;
                e.Id = source.Id;
                e.ZIndex = source.ZIndex;
                return e;
            }).ToList()
        };
    }

    private PackagingUnitRecord BuildSampleRecord()
    {
        var machine = _main.SelectedMachine ?? _main.Machines.FirstOrDefault();
        var article = _main.SelectedArticle ?? _main.Articles.FirstOrDefault();
        var machineNumber = machine?.Configuration.MachineNumber ?? 1;
        var machineName = machine?.Configuration.Name ?? "Spritzgussmaschine 01";
        var articleNumber = article?.ArticleNumber ?? "ART-4711";
        var description = article?.Description ?? "Musterteil Partcounter";
        var tool = article?.ToolNumber ?? "WZ-1001";
        var cavities = article?.ActiveCavities ?? (ushort)8;
        var target = article?.PackagingQuantity ?? 1000u;
        var cycles = cavities == 0 ? 0u : (uint)Math.Ceiling(target / (double)cavities);
        var actual = cycles * Math.Max((ushort)1, cavities);
        var now = DateTime.UtcNow;

        return new PackagingUnitRecord(
            $"VE-M{machineNumber:00}-0001-{now:yyyyMMddHHmmss}",
            machineNumber,
            machineName,
            1,
            string.IsNullOrWhiteSpace(_main.OrderNumber) ? "AUF-TEST-001" : _main.OrderNumber,
            articleNumber,
            description,
            tool,
            cavities,
            target,
            actual,
            actual >= target ? actual - target : 0,
            VeCompletionReason.AutomaticFull,
            now,
            "Preview",
            null);
    }

    private void ClampAllElements()
    {
        foreach (var row in Elements)
        {
            row.Model.WidthMm = Math.Min(row.Model.WidthMm, WidthMm);
            row.Model.HeightMm = Math.Min(row.Model.HeightMm, HeightMm);
            row.Model.Xmm = Math.Clamp(row.Model.Xmm, 0, Math.Max(0, WidthMm - row.Model.WidthMm));
            row.Model.Ymm = Math.Clamp(row.Model.Ymm, 0, Math.Max(0, HeightMm - row.Model.HeightMm));
            row.NotifyPositionChanged();
        }
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LabelElementEditorRow row && e.PropertyName is nameof(LabelElementEditorRow.WidthMm) or nameof(LabelElementEditorRow.HeightMm))
            MoveElement(row, row.Xmm, row.Ymm);
        if (!_suspendPreviewEvents)
            RaiseDesignerChanged();
    }

    private void ClearElementSubscriptions()
    {
        foreach (var row in Elements)
            row.PropertyChanged -= OnElementPropertyChanged;
    }

    private void RaiseDesignerChanged()
    {
        if (!_suspendPreviewEvents)
            DesignerChanged?.Invoke(this, EventArgs.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        private bool _running;
        public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke(parameter) ?? true);
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
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
