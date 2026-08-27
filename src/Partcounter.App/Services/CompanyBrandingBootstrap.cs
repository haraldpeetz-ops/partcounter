using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Partcounter.ViewModels;

namespace Partcounter.Services;

public sealed class CompanyBrandingBootstrap
{
    private static readonly Dictionary<MainWindow, CompanyBrandingBootstrap> Instances = new();

    private readonly MainWindow _window;
    private readonly CompanyBrandingService _service = CompanyBrandingService.Shared;
    private Border? _headerLogoBorder;
    private Image? _headerLogoImage;
    private Image? _settingsPreviewImage;
    private TextBlock? _settingsFileNameText;
    private TextBlock? _settingsStatusText;
    private INotifyPropertyChanged? _viewModelNotifier;

    private CompanyBrandingBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var bootstrap = new CompanyBrandingBootstrap(window);
        Instances[window] = bootstrap;
        bootstrap.Hook();
    }

    private void Hook()
    {
        _window.Title = "Partcounter R001.13";
        _window.Loaded += OnWindowLoaded;
        _window.Closed += OnWindowClosed;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _service.InitializeAsync();
            AttachHeaderLogo();
            AttachSettingsPanel();
            _service.Changed += OnBrandingChanged;

            if (_window.DataContext is INotifyPropertyChanged notifier)
            {
                _viewModelNotifier = notifier;
                notifier.PropertyChanged += OnMainViewModelPropertyChanged;
            }

            RefreshLogoUi();
            UpdateVersionBadge();
            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(UpdateVersionBadge));
        }
        catch (Exception ex)
        {
            if (_settingsStatusText is not null)
                _settingsStatusText.Text = $"Firmenlogo konnte nicht initialisiert werden: {ex.Message}";
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _service.Changed -= OnBrandingChanged;
        if (_viewModelNotifier is not null)
            _viewModelNotifier.PropertyChanged -= OnMainViewModelPropertyChanged;
        _window.Loaded -= OnWindowLoaded;
        _window.Closed -= OnWindowClosed;
        Instances.Remove(_window);
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
            _window.Dispatcher.BeginInvoke(new Action(UpdateVersionBadge));
    }

    private void OnBrandingChanged(object? sender, EventArgs e) =>
        _window.Dispatcher.BeginInvoke(new Action(RefreshLogoUi));

    private void AttachHeaderLogo()
    {
        if (_headerLogoBorder is not null)
            return;

        var titleText = FindDescendant<TextBlock>(_window, text => string.Equals(text.Text, "PARTCOUNTER", StringComparison.Ordinal));
        if (titleText?.Parent is not StackPanel titleStack || titleStack.Parent is not Grid headerGrid)
            return;

        if (headerGrid.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterCompanyBrandingHeader")))
            return;

        var column = Grid.GetColumn(titleStack);
        var row = Grid.GetRow(titleStack);
        var columnSpan = Grid.GetColumnSpan(titleStack);
        var rowSpan = Grid.GetRowSpan(titleStack);

        headerGrid.Children.Remove(titleStack);

        _headerLogoImage = new Image
        {
            MaxWidth = 190,
            MaxHeight = 52,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };

        _headerLogoBorder = new Border
        {
            MaxWidth = 200,
            Height = 56,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Child = _headerLogoImage
        };

        var brandedHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "PartcounterCompanyBrandingHeader"
        };
        brandedHeader.Children.Add(_headerLogoBorder);
        brandedHeader.Children.Add(titleStack);

        Grid.SetColumn(brandedHeader, column);
        Grid.SetRow(brandedHeader, row);
        Grid.SetColumnSpan(brandedHeader, columnSpan);
        Grid.SetRowSpan(brandedHeader, rowSpan);
        headerGrid.Children.Add(brandedHeader);
    }

    private void AttachSettingsPanel()
    {
        var settingsTab = FindDescendant<TabControl>(_window, control =>
            control.Items.OfType<TabItem>().Any(IsSettingsTab));
        var tab = settingsTab?.Items.OfType<TabItem>().FirstOrDefault(IsSettingsTab);
        if (tab?.Content is not StackPanel settingsStack)
            return;

        if (settingsStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterCompanyBrandingSettings")))
            return;

        _settingsPreviewImage = new Image
        {
            Width = 210,
            Height = 72,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var previewBorder = new Border
        {
            Width = 230,
            Height = 86,
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD8, 0xE0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8),
            Child = _settingsPreviewImage
        };

        _settingsFileNameText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        };

        _settingsStatusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var chooseButton = new Button
        {
            Content = "Firmenlogo auswählen / ersetzen",
            Padding = new Thickness(14, 8, 14, 8)
        };
        chooseButton.Click += OnChooseLogoClick;

        var removeButton = new Button
        {
            Content = "Firmenlogo entfernen",
            Padding = new Thickness(14, 8, 14, 8)
        };
        removeButton.Click += OnRemoveLogoClick;

        var buttons = new WrapPanel();
        buttons.Children.Add(chooseButton);
        buttons.Children.Add(removeButton);

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Firmenlogo / Leitstand",
            FontSize = 20,
            FontWeight = FontWeights.Bold
        });
        content.Children.Add(new TextBlock
        {
            Text = "Das Logo wird links oben in der Partcounter-Kopfzeile angezeigt. Die gewählte Datei wird in die Partcounter-Anwendungsdaten kopiert.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 5, 0, 10)
        });
        content.Children.Add(previewBorder);
        content.Children.Add(_settingsFileNameText);
        content.Children.Add(buttons);
        content.Children.Add(_settingsStatusText);

        var panel = new Border
        {
            Tag = "PartcounterCompanyBrandingSettings",
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 16),
            Child = content
        };

        settingsStack.Children.Insert(0, panel);
    }

    private async void OnChooseLogoClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Firmenlogo für Partcounter auswählen",
            Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(_window) != true)
            return;

        try
        {
            await _service.SetLogoAsync(dialog.FileName);
            if (_settingsStatusText is not null)
                _settingsStatusText.Text = "Firmenlogo gespeichert. Die Kopfzeile wurde aktualisiert.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Firmenlogo", MessageBoxButton.OK, MessageBoxImage.Error);
            if (_settingsStatusText is not null)
                _settingsStatusText.Text = $"Logo konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private async void OnRemoveLogoClick(object sender, RoutedEventArgs e)
    {
        if (!_service.HasLogo)
            return;

        var answer = MessageBox.Show(
            "Soll das Firmenlogo wirklich aus Partcounter entfernt werden?",
            "Firmenlogo entfernen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            await _service.RemoveLogoAsync();
            if (_settingsStatusText is not null)
                _settingsStatusText.Text = "Firmenlogo entfernt.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Firmenlogo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshLogoUi()
    {
        BitmapImage? bitmap = null;
        if (_service.HasLogo && !string.IsNullOrWhiteSpace(_service.CurrentLogoPath))
            bitmap = TryLoadBitmap(_service.CurrentLogoPath);

        if (_headerLogoImage is not null)
            _headerLogoImage.Source = bitmap;
        if (_headerLogoBorder is not null)
            _headerLogoBorder.Visibility = bitmap is null ? Visibility.Collapsed : Visibility.Visible;
        if (_settingsPreviewImage is not null)
            _settingsPreviewImage.Source = bitmap;
        if (_settingsFileNameText is not null)
            _settingsFileNameText.Text = bitmap is null
                ? "Kein Firmenlogo hinterlegt."
                : $"Aktuelles Logo: {_service.OriginalFileName}";
    }

    private void UpdateVersionBadge()
    {
        _window.Title = "Partcounter R001.13";
        var status = FindDescendant<TextBlock>(_window, text =>
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText")
                return true;
            return text.Text?.Contains("ECHTBETRIEB MODBUS TCP", StringComparison.OrdinalIgnoreCase) == true ||
                   text.Text?.Contains("SIMULATION", StringComparison.OrdinalIgnoreCase) == true;
        });

        if (status is null)
            return;

        var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
        BindingOperations.ClearBinding(status, TextBlock.TextProperty);
        status.Text = simulation
            ? "R001.13 · SIMULATION"
            : "R001.13 · ECHTBETRIEB MODBUS TCP";
    }

    private static BitmapImage? TryLoadBitmap(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSettingsTab(TabItem tab) =>
        tab.Header?.ToString()?.Contains("Einstellungen / Druck", StringComparison.OrdinalIgnoreCase) == true;

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
