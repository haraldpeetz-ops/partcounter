using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;

namespace Partcounter.Services;

public sealed class ProfessionalHelpBootstrap
{
    private static readonly Dictionary<MainWindow, ProfessionalHelpBootstrap> Instances = new();

    private readonly MainWindow _window;
    private INotifyPropertyChanged? _mainNotifier;

    private ProfessionalHelpBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var instance = new ProfessionalHelpBootstrap(window);
        Instances[window] = instance;
        window.Loaded += instance.OnLoaded;
        window.Closed += instance.OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_window.DataContext is INotifyPropertyChanged notifier)
        {
            _mainNotifier = notifier;
            _mainNotifier.PropertyChanged += OnMainPropertyChanged;
        }

        _window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(UpdateRevisionUi));
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
            _window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(UpdateRevisionUi));
    }

    private void UpdateRevisionUi()
    {
        _window.Title = AppVersionInfo.ProductTitle;

        foreach (var text in FindDescendants<TextBlock>(_window))
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText" ||
                text.Text?.Contains("· SIMULATION", StringComparison.OrdinalIgnoreCase) == true ||
                text.Text?.Contains("· ECHTBETRIEB", StringComparison.OrdinalIgnoreCase) == true)
            {
                BindingOperations.ClearBinding(text, TextBlock.TextProperty);
                var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
                text.Text = simulation
                    ? AppVersionInfo.SimulationStatus
                    : AppVersionInfo.ProductionStatus;
                continue;
            }

            if (text.Text?.StartsWith("Installiert:", StringComparison.OrdinalIgnoreCase) == true)
                text.Text = AppVersionInfo.InstalledText;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_mainNotifier is not null)
            _mainNotifier.PropertyChanged -= OnMainPropertyChanged;
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
}
