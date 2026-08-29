using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;

namespace Partcounter.Services;

public sealed class ProductionReadinessBootstrap
{
    private static readonly Dictionary<MainWindow, ProductionReadinessBootstrap> Instances = new();

    private readonly MainWindow _window;
    private readonly ProductionReadinessService _service = new();
    private TextBlock? _statusText;
    private TextBlock? _lastBackupText;
    private INotifyPropertyChanged? _viewModelNotifier;
    private DispatcherTimer? _settingsScrollTimer;

    private ProductionReadinessBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var instance = new ProductionReadinessBootstrap(window);
        Instances[window] = instance;
        instance.Hook();
    }

    private void Hook()
    {
        _window.Title = AppVersionInfo.ProductTitle;
        _window.Loaded += OnLoaded;
        _window.Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_window.DataContext is INotifyPropertyChanged notifier)
            {
                _viewModelNotifier = notifier;
                notifier.PropertyChanged += OnViewModelPropertyChanged;
            }

            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                AttachProductionReadinessPanel();
                NormalizeRevisionLabels();
                UpdateVersionBadge();
                RefreshLastBackupText();
                StartSettingsScrollGuard();
            }));

            await Task.Delay(400);
            var automaticBackup = await _service.EnsureDailyBackupAsync();
            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                RefreshLastBackupText();
                if (_statusText is not null)
                {
                    _statusText.Text = automaticBackup is null
                        ? "Produktionsbereitschaft aktiv · tägliche Datensicherung ist aktuell."
                        : $"Automatische Tagessicherung erstellt: {automaticBackup}";
                }
            }));
        }
        catch (Exception ex)
        {
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_statusText is not null)
                    _statusText.Text = $"Automatische Datensicherung konnte nicht ausgeführt werden: {ex.Message}";
            }));
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModelNotifier is not null)
            _viewModelNotifier.PropertyChanged -= OnViewModelPropertyChanged;
        if (_settingsScrollTimer is not null)
        {
            _settingsScrollTimer.Stop();
            _settingsScrollTimer = null;
        }
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        Instances.Remove(_window);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
        {
            _window.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() =>
            {
                NormalizeRevisionLabels();
                UpdateVersionBadge();
            }));
        }
    }

    private void AttachProductionReadinessPanel()
    {
        var tabControl = FindDescendant<TabControl>(_window, _ => true);
        var settingsTab = tabControl?.Items.OfType<TabItem>().FirstOrDefault(tab =>
            tab.Header?.ToString()?.Contains("Einstellungen / Druck", StringComparison.OrdinalIgnoreCase) == true);
        var settingsStack = GetSettingsStack(settingsTab);
        if (settingsStack is null)
            return;
        if (settingsStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterProductionReadiness")))
            return;

        _statusText = new TextBlock
        {
            Text = "Produktionsbereitschaft wird geprüft …",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66)),
            Margin = new Thickness(0, 8, 0, 0)
        };

        _lastBackupText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 5, 0, 0)
        };

        var checkButton = new Button
        {
            Content = "Datenbank prüfen",
            Padding = new Thickness(12, 7, 12, 7),
            ToolTip = "SQLite quick_check und Fremdschlüsselprüfung ausführen"
        };
        checkButton.Click += async (_, _) => await CheckDatabaseAsync();

        var backupButton = new Button
        {
            Content = "Sicherung jetzt erstellen",
            Padding = new Thickness(12, 7, 12, 7),
            ToolTip = "Konsistente Online-Sicherung der laufenden SQLite-Datenbank erstellen"
        };
        backupButton.Click += async (_, _) => await CreateBackupAsync();

        var openBackupButton = new Button
        {
            Content = "Sicherungsordner öffnen",
            Padding = new Thickness(12, 7, 12, 7)
        };
        openBackupButton.Click += (_, _) => OpenFolder(_service.BackupDirectory, "Sicherungsordner");

        var diagnosticButton = new Button
        {
            Content = "Diagnosepaket erstellen",
            Padding = new Thickness(12, 7, 12, 7),
            ToolTip = "Supportpaket ohne Settings-Tabelle und ohne Datenbanksicherung erstellen"
        };
        diagnosticButton.Click += async (_, _) => await CreateDiagnosticPackageAsync();

        var openDiagnosticButton = new Button
        {
            Content = "Diagnoseordner öffnen",
            Padding = new Thickness(12, 7, 12, 7)
        };
        openDiagnosticButton.Click += (_, _) => OpenFolder(_service.DiagnosticDirectory, "Diagnoseordner");

        var row1 = new WrapPanel();
        row1.Children.Add(checkButton);
        row1.Children.Add(backupButton);
        row1.Children.Add(openBackupButton);

        var row2 = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        row2.Children.Add(diagnosticButton);
        row2.Children.Add(openDiagnosticButton);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Datensicherung & Produktionsbereitschaft",
            FontSize = 20,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Partcounter erstellt automatisch einmal pro Kalendertag eine konsistente SQLite-Sicherung und behält die 30 jüngsten Sicherungen. Die Sicherung wird über die SQLite-Backup-API erzeugt und funktioniert auch bei aktiviertem WAL-Modus.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 5, 0, 10)
        });
        stack.Children.Add(row1);
        stack.Children.Add(row2);
        stack.Children.Add(_lastBackupText);
        stack.Children.Add(_statusText);
        stack.Children.Add(new TextBlock
        {
            Text = "Das Diagnosepaket enthält Systeminformationen, SQLite-Prüfergebnis, Startprotokoll und die letzten Ereignisse. Es exportiert keine Settings-Tabelle und keine Datenbanksicherung.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var panel = new Border
        {
            Tag = "PartcounterProductionReadiness",
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 16),
            Child = stack
        };

        settingsStack.Children.Insert(Math.Min(2, settingsStack.Children.Count), panel);
    }

    private void StartSettingsScrollGuard()
    {
        if (TryEnableSettingsScrolling())
            return;

        _settingsScrollTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.ApplicationIdle,
            (_, _) =>
            {
                NormalizeRevisionLabels();
                if (TryEnableSettingsScrolling() && _settingsScrollTimer is not null)
                {
                    _settingsScrollTimer.Stop();
                    _settingsScrollTimer = null;
                }
            },
            _window.Dispatcher);
        _settingsScrollTimer.Start();
    }

    private bool TryEnableSettingsScrolling()
    {
        var tabControl = FindDescendant<TabControl>(_window, _ => true);
        var settingsTab = tabControl?.Items.OfType<TabItem>().FirstOrDefault(tab =>
            tab.Header?.ToString()?.Contains("Einstellungen / Druck", StringComparison.OrdinalIgnoreCase) == true);
        if (settingsTab is null)
            return false;
        if (settingsTab.Content is ScrollViewer)
            return true;
        if (settingsTab.Content is not StackPanel settingsStack)
            return false;

        var hasBranding = settingsStack.Children.OfType<FrameworkElement>()
            .Any(x => Equals(x.Tag, "PartcounterCompanyBrandingSettings"));
        var hasUpdate = settingsStack.Children.OfType<FrameworkElement>()
            .Any(x => Equals(x.Tag, "PartcounterUpdateCenter") || Equals(x.Tag, "PartcounterR00114UpdateCenter"));
        var hasProductionReadiness = settingsStack.Children.OfType<FrameworkElement>()
            .Any(x => Equals(x.Tag, "PartcounterProductionReadiness") || Equals(x.Tag, "PartcounterR00115ProductionReadiness"));

        if (!hasBranding || !hasUpdate || !hasProductionReadiness)
            return false;

        settingsTab.Content = null;
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = settingsStack
        };
        settingsTab.Content = scrollViewer;
        return true;
    }

    private void NormalizeRevisionLabels()
    {
        foreach (var text in FindDescendants<TextBlock>(_window))
        {
            if (text.Text?.StartsWith("Installiert:", StringComparison.OrdinalIgnoreCase) == true)
                text.Text = AppVersionInfo.InstalledText;
        }
    }

    private async Task CheckDatabaseAsync()
    {
        if (_statusText is not null)
            _statusText.Text = "Datenbank wird geprüft …";

        try
        {
            var result = await _service.CheckDatabaseAsync();
            if (_statusText is not null)
                _statusText.Text = result.Summary;

            if (!result.IsOk)
            {
                MessageBox.Show(
                    result.Summary,
                    "Partcounter Datenbankprüfung",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"Datenbankprüfung fehlgeschlagen: {ex.Message}";
            MessageBox.Show(ex.Message, "Partcounter Datenbankprüfung", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateBackupAsync()
    {
        if (_statusText is not null)
            _statusText.Text = "Konsistente Datensicherung wird erstellt …";

        try
        {
            var path = await _service.CreateBackupAsync();
            RefreshLastBackupText();
            if (_statusText is not null)
                _statusText.Text = $"Datensicherung erfolgreich erstellt: {path}";
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"Datensicherung fehlgeschlagen: {ex.Message}";
            MessageBox.Show(ex.Message, "Partcounter Datensicherung", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateDiagnosticPackageAsync()
    {
        if (_statusText is not null)
            _statusText.Text = "Diagnosepaket wird erstellt …";

        try
        {
            var path = await _service.CreateDiagnosticPackageAsync();
            if (_statusText is not null)
                _statusText.Text = $"Diagnosepaket erstellt: {path}";
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"Diagnosepaket konnte nicht erstellt werden: {ex.Message}";
            MessageBox.Show(ex.Message, "Partcounter Diagnose", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshLastBackupText()
    {
        if (_lastBackupText is null)
            return;

        var latest = _service.GetLatestBackup();
        _lastBackupText.Text = latest is null
            ? $"Noch keine Sicherung vorhanden. Ziel: {_service.BackupDirectory}"
            : $"Letzte Sicherung: {latest.CreationTime:dd.MM.yyyy HH:mm:ss} · {latest.Length / 1024.0:N0} KB · {latest.FullName}";
    }

    private void OpenFolder(string path, string title)
    {
        try
        {
            ProductionReadinessService.OpenFolder(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateVersionBadge()
    {
        _window.Title = AppVersionInfo.ProductTitle;
        var status = FindDescendant<TextBlock>(_window, text =>
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText")
                return true;
            return text.Text?.Contains("SIMULATION", StringComparison.OrdinalIgnoreCase) == true ||
                   text.Text?.Contains("ECHTBETRIEB MODBUS TCP", StringComparison.OrdinalIgnoreCase) == true;
        });

        if (status is null)
            return;

        BindingOperations.ClearBinding(status, TextBlock.TextProperty);
        var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
        status.Text = simulation
            ? AppVersionInfo.SimulationStatus
            : AppVersionInfo.ProductionStatus;
    }

    private static StackPanel? GetSettingsStack(TabItem? tab) => tab?.Content switch
    {
        StackPanel stack => stack,
        ScrollViewer { Content: StackPanel stack } => stack,
        _ => null
    };

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var nested in FindDescendants<T>(child))
                yield return nested;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, Predicate<T> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && predicate(match))
                return match;

            var nested = FindDescendant(child, predicate);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
