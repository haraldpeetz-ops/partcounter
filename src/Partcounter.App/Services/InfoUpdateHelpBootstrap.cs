using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed class InfoUpdateHelpBootstrap
{
    private static readonly Dictionary<MainWindow, InfoUpdateHelpBootstrap> Instances = new();
    private readonly MainWindow _window;
    private readonly PartcounterUpdateService _updateService = new();
    private readonly DatabaseService _database = new();
    private HelpCenterWindow? _helpWindow;
    private TextBox? _networkPathBox;
    private TextBlock? _updateStatus;
    private TextBlock? _releaseNotes;
    private Button? _installButton;
    private PartcounterUpdatePackage? _selectedPackage;
    private INotifyPropertyChanged? _viewModelNotifier;

    private InfoUpdateHelpBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window)) return;
        var instance = new InfoUpdateHelpBootstrap(window);
        Instances[window] = instance;
        instance.Hook();
    }

    private void Hook()
    {
        _window.Title = "Partcounter R001.14";
        _window.Loaded += OnLoaded;
        _window.Closed += OnClosed;
        _window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _database.InitializeAsync();
            if (_window.DataContext is INotifyPropertyChanged notifier)
            {
                _viewModelNotifier = notifier;
                notifier.PropertyChanged += OnViewModelPropertyChanged;
            }

            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                AttachHeaderButtons();
                await AttachUpdatePanelAsync();
                UpdateVersionBadge();
            }));
        }
        catch
        {
            // Hilfe/Über/Update dürfen den regulären Programmstart nicht blockieren.
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModelNotifier is not null)
            _viewModelNotifier.PropertyChanged -= OnViewModelPropertyChanged;
        if (_helpWindow is not null)
            _helpWindow.Close();
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        _window.PreviewKeyDown -= OnPreviewKeyDown;
        Instances.Remove(_window);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(UpdateVersionBadge));
    }

    private void AttachHeaderButtons()
    {
        var modeButton = FindDescendant<Button>(_window, button =>
        {
            var expression = BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty);
            return expression?.ParentBinding.Path?.Path == "OperatingModeButtonText";
        });
        if (modeButton?.Parent is not StackPanel rightStack)
            return;
        if (rightStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterR00114HelpButton")))
            return;

        var help = new Button
        {
            Content = "Hilfe (F1)",
            Tag = "PartcounterR00114HelpButton",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Ausführliche Partcounter-Hilfe mit Funktionsabhängigkeiten öffnen"
        };
        help.Click += (_, _) => OpenHelp();

        var about = new Button
        {
            Content = "Über",
            Tag = "PartcounterR00114AboutButton",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Version, Programmierer, Lizenz- und Systeminformationen"
        };
        about.Click += (_, _) => new AboutWindow { Owner = _window }.ShowDialog();

        rightStack.Children.Add(help);
        rightStack.Children.Add(about);
    }

    private async Task AttachUpdatePanelAsync()
    {
        var tabControl = FindDescendant<TabControl>(_window, _ => true);
        var settingsTab = tabControl?.Items.OfType<TabItem>().FirstOrDefault(tab =>
            tab.Header?.ToString()?.Contains("Einstellungen / Druck", StringComparison.OrdinalIgnoreCase) == true);
        if (settingsTab?.Content is not StackPanel settingsStack)
            return;
        if (settingsStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterR00114UpdateCenter")))
            return;

        _networkPathBox = new TextBox
        {
            MinHeight = 30,
            Text = await _database.GetSettingAsync("UpdateNetworkPath") ?? string.Empty,
            ToolTip = "UNC-Pfad oder lokaler Ordner mit Partcounter-Updatepaketen, z. B. \\\\server\\software\\Partcounter"
        };
        _updateStatus = new TextBlock
        {
            Text = $"Installiert: R001.14 / {_updateService.CurrentVersion}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66)),
            Margin = new Thickness(0, 8, 0, 0)
        };
        _releaseNotes = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            Margin = new Thickness(0, 5, 0, 0)
        };

        var savePath = new Button { Content = "Netzwerkpfad speichern", Padding = new Thickness(12, 6, 12, 6) };
        savePath.Click += async (_, _) => await SaveNetworkPathAsync();
        var checkNetwork = new Button { Content = "Netzwerk nach Update durchsuchen", Padding = new Thickness(12, 6, 12, 6) };
        checkNetwork.Click += async (_, _) => await CheckNetworkAsync();
        var local = new Button { Content = "Updatepaket von USB / lokaler Datei", Padding = new Thickness(12, 6, 12, 6) };
        local.Click += async (_, _) => await SelectLocalPackageAsync();
        _installButton = new Button
        {
            Content = "Ausgewähltes Update installieren",
            Padding = new Thickness(12, 7, 12, 7),
            FontWeight = FontWeights.SemiBold,
            IsEnabled = false
        };
        _installButton.Click += async (_, _) => await InstallSelectedAsync();

        var row1 = new WrapPanel();
        row1.Children.Add(savePath);
        row1.Children.Add(checkNetwork);
        var row2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        row2.Children.Add(local);
        row2.Children.Add(_installButton);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Software-Update", FontSize = 20, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock
        {
            Text = "Updates können aus einem Netzwerkordner, von USB oder aus einer lokalen ZIP-Datei eingespielt werden. Partcounter prüft Manifest, Version und SHA-256-Dateiprüfsummen vor der Installation.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 5, 0, 10)
        });
        stack.Children.Add(new TextBlock { Text = "Netzwerk-Updatepfad", FontWeight = FontWeights.SemiBold });
        stack.Children.Add(_networkPathBox);
        stack.Children.Add(row1);
        stack.Children.Add(row2);
        stack.Children.Add(_updateStatus);
        stack.Children.Add(_releaseNotes);
        stack.Children.Add(new TextBlock
        {
            Text = $"Update-Arbeitsbereich: {_updateService.UpdateRoot}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var panel = new Border
        {
            Tag = "PartcounterR00114UpdateCenter",
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 16),
            Child = stack
        };

        var insertIndex = Math.Min(1, settingsStack.Children.Count);
        settingsStack.Children.Insert(insertIndex, panel);
    }

    private async Task SaveNetworkPathAsync()
    {
        if (_networkPathBox is null || _updateStatus is null) return;
        var path = Environment.ExpandEnvironmentVariables(_networkPathBox.Text.Trim());
        await _database.SetSettingAsync("UpdateNetworkPath", path);
        _networkPathBox.Text = path;
        _updateStatus.Text = string.IsNullOrWhiteSpace(path)
            ? "Netzwerk-Updatepfad wurde geleert."
            : $"Netzwerk-Updatepfad gespeichert: {path}";
    }

    private async Task CheckNetworkAsync()
    {
        if (_networkPathBox is null || _updateStatus is null) return;
        try
        {
            var path = Environment.ExpandEnvironmentVariables(_networkPathBox.Text.Trim());
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Bitte zuerst einen Netzwerk-Updatepfad eintragen.");
            _updateStatus.Text = $"Prüfe Updatepakete in {path} …";
            var package = await _updateService.FindLatestPackageAsync(path);
            if (package is null)
            {
                SelectPackage(null);
                _updateStatus.Text = $"Kein neueres gültiges Partcounter-Update in {path} gefunden.";
                return;
            }
            SelectPackage(package);
        }
        catch (Exception ex)
        {
            SelectPackage(null);
            _updateStatus.Text = $"Netzwerkprüfung fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task SelectLocalPackageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Partcounter-Updatepaket auswählen",
            Filter = "Partcounter Update (*.zip)|*.zip|ZIP-Dateien (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(_window) != true)
            return;

        try
        {
            if (_updateStatus is not null)
                _updateStatus.Text = "Updatepaket wird geprüft …";
            var package = await _updateService.InspectPackageAsync(dialog.FileName);
            SelectPackage(package);
        }
        catch (Exception ex)
        {
            SelectPackage(null);
            if (_updateStatus is not null)
                _updateStatus.Text = $"Updatepaket ungültig: {ex.Message}";
        }
    }

    private void SelectPackage(PartcounterUpdatePackage? package)
    {
        _selectedPackage = package;
        if (_installButton is not null)
            _installButton.IsEnabled = package?.IsNewer == true;
        if (_releaseNotes is not null)
            _releaseNotes.Text = package is null ? string.Empty : package.Manifest.ReleaseNotes;
        if (_updateStatus is null || package is null) return;

        _updateStatus.Text = package.IsNewer
            ? $"Update verfügbar: {package.Manifest.Revision} / {package.TargetVersion} · {package.PayloadFileCount} geprüfte Dateien · {Path.GetFileName(package.PackagePath)}"
            : $"Paket geprüft, aber nicht neuer: {package.Manifest.Revision} / {package.TargetVersion}; installiert ist {_updateService.CurrentVersion}.";
    }

    private async Task InstallSelectedAsync()
    {
        if (_selectedPackage is null || !_selectedPackage.IsNewer) return;
        var answer = MessageBox.Show(
            $"Partcounter wird auf {_selectedPackage.Manifest.Revision} / {_selectedPackage.TargetVersion} aktualisiert.\n\n" +
            "Die Anwendung wird beendet, die zu ersetzenden Programmdateien werden gesichert und anschließend wird Partcounter neu gestartet. Produktionsdaten unter %LOCALAPPDATA%\\Partcounter bleiben erhalten.\n\nUpdate jetzt installieren?",
            "Partcounter Software-Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            if (_updateStatus is not null)
                _updateStatus.Text = "Update wird vorbereitet und geprüft …";
            var backup = await _updateService.StageAndScheduleInstallAsync(_selectedPackage);
            MessageBox.Show(
                $"Das Update ist vorbereitet. Partcounter wird jetzt beendet und nach der Installation automatisch neu gestartet.\n\nBackup: {backup}",
                "Partcounter Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (_updateStatus is not null)
                _updateStatus.Text = $"Update konnte nicht vorbereitet werden: {ex.Message}";
            MessageBox.Show(ex.Message, "Partcounter Update", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenHelp(string? topicId = null)
    {
        if (_helpWindow is null || !_helpWindow.IsLoaded)
        {
            _helpWindow = new HelpCenterWindow { Owner = _window };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
        }
        else
        {
            _helpWindow.Show();
            _helpWindow.Activate();
        }
        if (!string.IsNullOrWhiteSpace(topicId))
            _helpWindow.OpenTopic(topicId);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            OpenHelp();
            e.Handled = true;
        }
    }

    private void UpdateVersionBadge()
    {
        _window.Title = "Partcounter R001.14";
        var status = FindDescendant<TextBlock>(_window, text =>
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText") return true;
            return text.Text?.Contains("SIMULATION", StringComparison.OrdinalIgnoreCase) == true ||
                   text.Text?.Contains("ECHTBETRIEB MODBUS TCP", StringComparison.OrdinalIgnoreCase) == true;
        });
        if (status is null) return;
        BindingOperations.ClearBinding(status, TextBlock.TextProperty);
        var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
        status.Text = simulation ? "R001.14 · SIMULATION" : "R001.14 · ECHTBETRIEB MODBUS TCP";
    }

    private static T? FindDescendant<T>(DependencyObject root, Predicate<T> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && predicate(match)) return match;
            var nested = FindDescendant(child, predicate);
            if (nested is not null) return nested;
        }
        return null;
    }
}
