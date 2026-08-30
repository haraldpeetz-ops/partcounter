using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed class LiveCommissioningBootstrap
{
    private static readonly Dictionary<MainWindow, LiveCommissioningBootstrap> Instances = new();

    private readonly MainWindow _window;
    private LiveCommissioningViewModel? _viewModel;
    private INotifyPropertyChanged? _mainNotifier;

    private LiveCommissioningBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var instance = new LiveCommissioningBootstrap(window);
        Instances[window] = instance;
        window.Loaded += instance.OnLoaded;
        window.Closed += instance.OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_window.DataContext is not MainViewModel main)
                return;

            _mainNotifier = main;
            _mainNotifier.PropertyChanged += OnMainPropertyChanged;

            // MainWindow adds the protected commissioning tab asynchronously during its own Loaded handler.
            // Wait for that tab instead of adding a second unprotected top-level administration area.
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (TryAttachToCommissioning(main))
                    break;

                await Task.Delay(150);
            }

            UpdateRevisionUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Die R001.16 Live-Abnahme konnte nicht initialisiert werden.\n\n{ex.Message}",
                "Partcounter R001.16",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private bool TryAttachToCommissioning(MainViewModel main)
    {
        var mainTabs = FindDescendant<TabControl>(_window, tabs =>
            tabs.Items.OfType<TabItem>().Any(tab =>
                string.Equals(tab.Header?.ToString(), "Leitstand", StringComparison.Ordinal)));

        var commissioningTab = mainTabs?.Items.OfType<TabItem>().FirstOrDefault(tab =>
            string.Equals(tab.Header?.ToString(), "Inbetriebnahme / Diagnose", StringComparison.Ordinal));

        if (commissioningTab?.Content is not CommissioningView commissioningView)
            return false;

        var innerTabs = FindDescendant<TabControl>(commissioningView, _ => true);
        if (innerTabs is null)
            return false;

        if (innerTabs.Items.OfType<TabItem>().Any(tab =>
                string.Equals(tab.Header?.ToString(), "Live-Abnahme R001.16", StringComparison.Ordinal)))
            return true;

        _viewModel = new LiveCommissioningViewModel(main);
        var view = new LiveCommissioningView { DataContext = _viewModel };
        innerTabs.Items.Add(new TabItem
        {
            Header = "Live-Abnahme R001.16",
            Content = view
        });

        _ = InitializeViewModelAsync(_viewModel);
        return true;
    }

    private static async Task InitializeViewModelAsync(LiveCommissioningViewModel viewModel)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Live-Abnahme konnte nicht initialisiert werden.\n\n{ex.Message}",
                "Partcounter R001.16",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
        {
            // ProductionReadinessBootstrap still normalizes legacy R001.15 labels. Queue at the
            // same low priority after its handler so R001.16 remains the final visible revision.
            _ = _window.Dispatcher.BeginInvoke(
                DispatcherPriority.SystemIdle,
                new Action(UpdateRevisionUi));
        }
    }

    private void UpdateRevisionUi()
    {
        _window.Title = "Partcounter R001.16";

        foreach (var text in FindDescendants<TextBlock>(_window))
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            var isRevisionStatus = expression?.ParentBinding.Path?.Path == "SystemStatusText" ||
                                   text.Text?.Contains("ECHTBETRIEB MODBUS TCP", StringComparison.OrdinalIgnoreCase) == true ||
                                   (text.Text?.Contains("SIMULATION", StringComparison.OrdinalIgnoreCase) == true &&
                                    text.Text.StartsWith("R001.", StringComparison.Ordinal));

            if (isRevisionStatus)
            {
                BindingOperations.ClearBinding(text, TextBlock.TextProperty);
                var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
                text.Text = simulation
                    ? "R001.16 · SIMULATION"
                    : "R001.16 · ECHTBETRIEB MODBUS TCP";
                continue;
            }

            if (text.Text?.StartsWith("Installiert: R001.15 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.15 /", "Installiert: R001.16 /", StringComparison.Ordinal);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_mainNotifier is not null)
            _mainNotifier.PropertyChanged -= OnMainPropertyChanged;
        _viewModel?.Dispose();
        _viewModel = null;
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        Instances.Remove(_window);
    }

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
