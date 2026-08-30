using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Partcounter.Services;
using Partcounter.ViewModels;

namespace Partcounter.Views;

public sealed class SupportCenterWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel? _viewModel;
    private readonly ProductionReadinessService _readiness = new();
    private readonly SupportDiagnosticService _diagnostics;

    private readonly TextBlock _operatingMode = new();
    private readonly TextBlock _databaseHealth = new();
    private readonly TextBlock _backupStatus = new();
    private readonly TextBlock _actionStatus = new();

    public SupportCenterWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _viewModel = mainWindow.DataContext as MainViewModel;
        _diagnostics = new SupportDiagnosticService(_readiness);

        Owner = mainWindow;
        Title = $"{AppVersionInfo.ProductTitle} – Bedienung & Support";
        Width = 1120;
        Height = 820;
        MinWidth = 900;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));
        Content = BuildUi();

        Loaded += async (_, _) => await RefreshSystemStateAsync();
        Closed += OnClosed;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private UIElement BuildUi()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var root = new StackPanel { Margin = new Thickness(18) };
        scroll.Content = root;

        root.Children.Add(new TextBlock
        {
            Text = "PARTCOUNTER · BEDIENUNG & SUPPORT",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B))
        });
        root.Children.Add(new TextBlock
        {
            Text = "Schnellhilfe, Systemzustand, Datenbankprüfung, Sicherung und Supportpaket an einer Stelle.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 14)
        });

        root.Children.Add(BuildSystemCard());
        root.Children.Add(BuildSupportActionsCard());
        root.Children.Add(BuildGuidedHelpCard());
        root.Children.Add(BuildPathsCard());

        return scroll;
    }

    private Border BuildSystemCard()
    {
        _operatingMode.TextWrapping = TextWrapping.Wrap;
        _databaseHealth.TextWrapping = TextWrapping.Wrap;
        _backupStatus.TextWrapping = TextWrapping.Wrap;

        var grid = InfoGrid(new[]
        {
            ("Revision", AppVersionInfo.Revision),
            ("Programmversion", AppVersionInfo.VersionText),
            ("Build", AppVersionInfo.InformationalVersion),
            ("Windows / OS", RuntimeInformation.OSDescription),
            (".NET Runtime", RuntimeInformation.FrameworkDescription),
            ("Architektur", RuntimeInformation.ProcessArchitecture.ToString())
        });

        var stack = new StackPanel();
        stack.Children.Add(Heading("Systemzustand"));
        stack.Children.Add(grid);
        stack.Children.Add(LabelValue("Betriebszustand", _operatingMode));
        stack.Children.Add(LabelValue("Datenbank", _databaseHealth));
        stack.Children.Add(LabelValue("Letzte Sicherung", _backupStatus));

        var refresh = new Button
        {
            Content = "Systemprüfung aktualisieren",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        refresh.Click += async (_, _) => await RefreshSystemStateAsync();
        stack.Children.Add(refresh);

        return Card(stack);
    }

    private Border BuildSupportActionsCard()
    {
        _actionStatus.Text = "Bereit.";
        _actionStatus.TextWrapping = TextWrapping.Wrap;
        _actionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66));
        _actionStatus.Margin = new Thickness(0, 8, 0, 0);

        var row = new WrapPanel();
        row.Children.Add(ActionButton("Datenbank prüfen", async () => await CheckDatabaseAsync()));
        row.Children.Add(ActionButton("Sicherung jetzt erstellen", async () => await CreateBackupAsync()));
        row.Children.Add(ActionButton("Supportpaket erstellen", async () => await CreateSupportPackageAsync(), true));

        var row2 = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        row2.Children.Add(ActionButton("Supportinfo kopieren", () => CopySupportInfo()));
        row2.Children.Add(ActionButton("Diagnoseordner öffnen", () => OpenFolder(_readiness.DiagnosticDirectory)));
        row2.Children.Add(ActionButton("Startprotokoll öffnen", OpenStartupLog));

        var stack = new StackPanel();
        stack.Children.Add(Heading("Supportaktionen"));
        stack.Children.Add(new TextBlock
        {
            Text = "Das Supportpaket enthält keine Datenbanksicherung und exportiert keine Settings-Tabelle. Die Paketkennung wird automatisch aus der tatsächlich laufenden Programmversion erzeugt.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 0, 0, 9)
        });
        stack.Children.Add(row);
        stack.Children.Add(row2);
        stack.Children.Add(_actionStatus);
        return Card(stack);
    }

    private Border BuildGuidedHelpCard()
    {
        var row = new WrapPanel();
        row.Children.Add(HelpButton("Leitstand / Auftrag", "DASH-01"));
        row.Children.Add(HelpButton("VE-Historie / Reprint", "HISTORY-01"));
        row.Children.Add(HelpButton("Modbus / Maschine offline", "MODBUS-02"));
        row.Children.Add(HelpButton("ARBURG ALS", "ALS-01"));
        row.Children.Add(HelpButton("Etiketteneditor", "LABEL-01"));
        row.Children.Add(HelpButton("Inbetriebnahme", "COMMISSION-01"));
        row.Children.Add(HelpButton("Einstellungen / Druck", "SETTINGS-01"));

        var stack = new StackPanel();
        stack.Children.Add(Heading("Geführte Bedien- und Fehlerhilfe"));
        stack.Children.Add(new TextBlock
        {
            Text = "Direkter Einstieg in die häufigsten Bedien- und Supportthemen. F1 im Hauptfenster bleibt zusätzlich kontextbezogen verfügbar.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        stack.Children.Add(row);
        return Card(stack);
    }

    private Border BuildPathsCard()
    {
        var row = new WrapPanel();
        row.Children.Add(ActionButton("Partcounter-Daten", () => OpenFolder(_readiness.DataDirectory)));
        row.Children.Add(ActionButton("Sicherungen", () => OpenFolder(_readiness.BackupDirectory)));
        row.Children.Add(ActionButton("Diagnosen", () => OpenFolder(_readiness.DiagnosticDirectory)));
        row.Children.Add(ActionButton("Hilfe-Screenshots", () => OpenFolder(DocumentationCaptureService.ScreenshotDirectory)));

        var stack = new StackPanel();
        stack.Children.Add(Heading("Ordner & Nachweise"));
        stack.Children.Add(row);
        return Card(stack);
    }

    private async Task RefreshSystemStateAsync()
    {
        RefreshOperatingMode();
        _databaseHealth.Text = "Prüfung läuft …";

        try
        {
            var health = await _readiness.CheckDatabaseAsync();
            _databaseHealth.Text = health.IsOk ? $"OK · {health.Summary}" : $"WARNUNG · {health.Summary}";
        }
        catch (Exception ex)
        {
            _databaseHealth.Text = $"Prüfung fehlgeschlagen: {ex.Message}";
        }

        RefreshBackupStatus();
    }

    private async Task CheckDatabaseAsync()
    {
        _actionStatus.Text = "Datenbank wird geprüft …";
        try
        {
            var result = await _readiness.CheckDatabaseAsync();
            _databaseHealth.Text = result.IsOk ? $"OK · {result.Summary}" : $"WARNUNG · {result.Summary}";
            _actionStatus.Text = result.Summary;
        }
        catch (Exception ex)
        {
            _actionStatus.Text = $"Datenbankprüfung fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task CreateBackupAsync()
    {
        _actionStatus.Text = "Sicherung wird erstellt …";
        try
        {
            var path = await _readiness.CreateBackupAsync();
            _actionStatus.Text = $"Sicherung erfolgreich erstellt: {path}";
            RefreshBackupStatus();
        }
        catch (Exception ex)
        {
            _actionStatus.Text = $"Sicherung fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task CreateSupportPackageAsync()
    {
        _actionStatus.Text = "Supportpaket wird erstellt …";
        try
        {
            var path = await _diagnostics.CreateCurrentVersionPackageAsync();
            _actionStatus.Text = $"Supportpaket erfolgreich erstellt: {path}";
        }
        catch (Exception ex)
        {
            _actionStatus.Text = $"Supportpaket konnte nicht erstellt werden: {ex.Message}";
        }
    }

    private void CopySupportInfo()
    {
        Clipboard.SetText(BuildSupportInfo());
        _actionStatus.Text = "Supportinformationen wurden in die Zwischenablage kopiert.";
    }

    private string BuildSupportInfo()
    {
        var latest = _readiness.GetLatestBackup();
        var sb = new StringBuilder();
        sb.AppendLine("PARTCOUNTER SUPPORTINFO");
        sb.AppendLine($"Revision: {AppVersionInfo.Revision}");
        sb.AppendLine($"Version: {AppVersionInfo.VersionText}");
        sb.AppendLine($"Build: {AppVersionInfo.InformationalVersion}");
        sb.AppendLine($"Betrieb: {GetOperatingModeText()}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Architektur: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Datenbankstatus: {_databaseHealth.Text}");
        sb.AppendLine($"Letzte Sicherung: {(latest is null ? "keine" : latest.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"))}");
        sb.AppendLine($"Diagnoseordner: {_readiness.DiagnosticDirectory}");
        sb.AppendLine("Hinweis: Diese Kurzinfo enthält keine Passwörter, Tokens, API-Keys oder ALS-Zugangsdaten.");
        return sb.ToString();
    }

    private void OpenStartupLog()
    {
        if (!File.Exists(_readiness.StartupLogPath))
        {
            _actionStatus.Text = $"Noch kein Startprotokoll vorhanden: {_readiness.StartupLogPath}";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _readiness.StartupLogPath,
            UseShellExecute = true
        });
    }

    private void OpenHelp(string topicId)
    {
        var help = new HelpCenterWindow { Owner = _mainWindow };
        help.OpenTopic(topicId);
        help.Show();
        VersionUiService.NormalizeWindow(help);
    }

    private void OpenFolder(string path)
    {
        try
        {
            ProductionReadinessService.OpenFolder(path);
        }
        catch (Exception ex)
        {
            _actionStatus.Text = $"Ordner konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    private void RefreshOperatingMode() => _operatingMode.Text = GetOperatingModeText();

    private string GetOperatingModeText()
    {
        if (_viewModel is null)
            return "nicht verfügbar";
        return _viewModel.IsSimulationMode
            ? $"SIMULATION · {_viewModel.SystemStatusText}"
            : $"ECHTBETRIEB MODBUS TCP · {_viewModel.SystemStatusText}";
    }

    private void RefreshBackupStatus()
    {
        var latest = _readiness.GetLatestBackup();
        _backupStatus.Text = latest is null
            ? "Keine Sicherung gefunden."
            : $"{latest.CreationTime:yyyy-MM-dd HH:mm:ss} · {latest.Name}";
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
            _ = Dispatcher.BeginInvoke(new Action(RefreshOperatingMode));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Closed -= OnClosed;
    }

    private Button HelpButton(string text, string topicId)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 6, 6)
        };
        button.Click += (_, _) => OpenHelp(topicId);
        return button;
    }

    private Button ActionButton(string text, Action action, bool emphasized = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 6, 6),
            FontWeight = emphasized ? FontWeights.SemiBold : FontWeights.Normal
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button ActionButton(string text, Func<Task> action, bool emphasized = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 6, 6),
            FontWeight = emphasized ? FontWeights.SemiBold : FontWeights.Normal
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static Border LabelValue(string label, TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labelText = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 10, 2)
        };
        Grid.SetColumn(value, 1);
        value.Margin = new Thickness(0, 2, 0, 2);
        grid.Children.Add(labelText);
        grid.Children.Add(value);
        return new Border { Child = grid };
    }

    private static Grid InfoGrid(IEnumerable<(string Label, string Value)> rows)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rowIndex = 0;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = row.Label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 3, 10, 3)
            };
            var value = new TextBlock
            {
                Text = row.Value,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66)),
                Margin = new Thickness(0, 3, 0, 3)
            };
            Grid.SetRow(label, rowIndex);
            Grid.SetRow(value, rowIndex);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
            rowIndex++;
        }
        return grid;
    }

    private static Border Card(UIElement child) => new()
    {
        Background = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(14),
        Margin = new Thickness(0, 0, 0, 12),
        Child = child
    };
}
