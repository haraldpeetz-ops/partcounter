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
        _window.Title = "Partcounter R001.20";

        foreach (var text in FindDescendants<TextBlock>(_window))
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText" ||
                text.Text?.StartsWith("R001.19 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.19 · ECHTBETRIEB", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.18 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.18 · ECHTBETRIEB", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.17 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.17 · ECHTBETRIEB", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.16 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.16 · ECHTBETRIEB", StringComparison.Ordinal) == true)
            {
                BindingOperations.ClearBinding(text, TextBlock.TextProperty);
                var simulation = _window.DataContext is MainViewModel vm && vm.IsSimulationMode;
                text.Text = simulation
                    ? "R001.20 · SIMULATION"
                    : "R001.20 · ECHTBETRIEB MODBUS TCP";
                continue;
            }

            if (text.Text?.StartsWith("Installiert: R001.19 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.19 /", "Installiert: R001.20 /", StringComparison.Ordinal);
            else if (text.Text?.StartsWith("Installiert: R001.18 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.18 /", "Installiert: R001.20 /", StringComparison.Ordinal);
            else if (text.Text?.StartsWith("Installiert: R001.17 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.17 /", "Installiert: R001.20 /", StringComparison.Ordinal);
            else if (text.Text?.StartsWith("Installiert: R001.16 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.16 /", "Installiert: R001.20 /", StringComparison.Ordinal);
            else if (text.Text?.StartsWith("Installiert: R001.14 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.14 /", "Installiert: R001.20 /", StringComparison.Ordinal);
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
