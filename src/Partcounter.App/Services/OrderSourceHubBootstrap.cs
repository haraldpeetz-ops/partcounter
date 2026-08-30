using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed class OrderSourceHubBootstrap
{
    private const string AlsSource = OrderSourceCoordinator.AlsDisplayName;
    private const string ProAlphaSource = OrderSourceCoordinator.ProAlphaDisplayName;
    private static readonly Dictionary<MainWindow, OrderSourceHubBootstrap> Instances = new();

    private readonly MainWindow _window;
    private readonly DatabaseService _database = new();
    private DispatcherTimer? _timer;
    private ProAlphaViewModel? _proAlphaViewModel;
    private bool _done;

    private OrderSourceHubBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window)) return;
        var instance = new OrderSourceHubBootstrap(window);
        Instances[window] = instance;
        window.Loaded += instance.OnLoaded;
        window.Closed += instance.OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.ApplicationIdle,
            async (_, _) => await TryAttachAsync(),
            _window.Dispatcher);
        _timer.Start();
    }

    private async Task TryAttachAsync()
    {
        if (_done) return;

        var alsTab = FindDescendant<TabItem>(_window, tab =>
            tab.Content is AlsIntegrationView ||
            (tab.Header?.ToString()?.Contains("ARBURG ALS", StringComparison.OrdinalIgnoreCase) == true &&
             FindDescendant<AlsIntegrationView>(tab, _ => true) is not null));
        if (alsTab is null || alsTab.Content is null) return;

        var alsContent = alsTab.Content as FrameworkElement;
        if (alsContent is null || alsContent.DataContext is not AlsViewModel alsViewModel) return;
        if (_window.DataContext is not MainViewModel main) return;

        _proAlphaViewModel = new ProAlphaViewModel(main);
        await _proAlphaViewModel.InitializeAsync();

        var proAlphaView = new ProAlphaIntegrationView { DataContext = _proAlphaViewModel };
        var alsExtendedView = new AlsExtendedAccessView { DataContext = alsViewModel };
        var innerTabs = new TabControl { Margin = new Thickness(0, 8, 0, 0) };
        var alsInner = new TabItem { Header = AlsSource, Content = alsContent };
        var alsAccessInner = new TabItem { Header = "ALS Zugang erweitert", Content = alsExtendedView };
        var proAlphaInner = new TabItem { Header = ProAlphaSource, Content = proAlphaView };
        innerTabs.Items.Add(alsInner);
        innerTabs.Items.Add(alsAccessInner);
        innerTabs.Items.Add(proAlphaInner);

        var activeCombo = new ComboBox
        {
            Width = 220,
            MinHeight = 30,
            ItemsSource = new[] { AlsSource, ProAlphaSource },
            Margin = new Thickness(8, 0, 0, 0)
        };
        var status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66))
        };

        var topRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        topRow.Children.Add(new TextBlock
        {
            Text = "Aktive Auftragsquelle",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.Bold
        });
        topRow.Children.Add(activeCombo);
        topRow.Children.Add(status);

        var info = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF7)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD4, 0xDE)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Children =
                {
                    topRow,
                    new TextBlock
                    {
                        Text = "Nur eine Quelle soll im Regelbetrieb als führende Auftragsquelle verwendet werden. Beide Profile können unabhängig getestet und konfiguriert werden; die Auswahl wird dauerhaft gespeichert. 'ALS Zugang erweitert' enthält zusätzliche OAuth2-/Proxy- und Preflight-Felder, ändert aber die führende Quelle nicht.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };

        var root = new DockPanel { Margin = new Thickness(2) };
        DockPanel.SetDock(info, Dock.Top);
        root.Children.Add(info);
        root.Children.Add(innerTabs);

        alsTab.Content = root;
        alsTab.Header = "Auftragsquellen · ARBURG ALS / proALPHA";

        var activeKind = await OrderSourceCoordinator.GetActiveAsync(_database);
        var active = activeKind == OrderSourceKind.ProAlpha ? ProAlphaSource : AlsSource;
        activeCombo.SelectedItem = active;
        innerTabs.SelectedItem = active == ProAlphaSource ? proAlphaInner : alsInner;
        status.Text = $"Führend: {active}";

        activeCombo.SelectionChanged += async (_, _) =>
        {
            var selected = activeCombo.SelectedItem?.ToString() == ProAlphaSource ? ProAlphaSource : AlsSource;
            await OrderSourceCoordinator.SetActiveAsync(_database, selected == ProAlphaSource ? OrderSourceKind.ProAlpha : OrderSourceKind.ArburgAls);
            status.Text = $"Führend: {selected}";
            innerTabs.SelectedItem = selected == ProAlphaSource ? proAlphaInner : alsInner;
        };

        innerTabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, innerTabs)) return;
            var opened = (innerTabs.SelectedItem as TabItem)?.Header?.ToString();
            status.Text = $"Führend: {activeCombo.SelectedItem} · geöffnet: {opened}";
        };

        _done = true;
        _timer?.Stop();
        _timer = null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        _proAlphaViewModel?.Dispose();
        _proAlphaViewModel = null;
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        Instances.Remove(_window);
    }

    private static T? FindDescendant<T>(DependencyObject root, Predicate<T> predicate) where T : DependencyObject
    {
        if (root is T self && predicate(self)) return self;
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
