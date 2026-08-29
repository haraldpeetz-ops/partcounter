using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed class SupportCenterBootstrap
{
    private static readonly Dictionary<MainWindow, SupportCenterBootstrap> Instances = new();
    private readonly MainWindow _window;
    private SupportCenterWindow? _supportWindow;

    private SupportCenterBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var instance = new SupportCenterBootstrap(window);
        Instances[window] = instance;
        window.Loaded += instance.OnLoaded;
        window.Closed += instance.OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(AttachButton));
    }

    private void AttachButton()
    {
        var modeButton = FindDescendant<Button>(_window, button =>
        {
            var expression = BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty);
            return expression?.ParentBinding.Path?.Path == "OperatingModeButtonText";
        });

        if (modeButton?.Parent is not Panel parent)
            return;

        if (parent.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "PartcounterSupportCenterButton")))
            return;

        var button = new Button
        {
            Content = "Bedienung & Support",
            Tag = "PartcounterSupportCenterButton",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Supportzentrum mit Schnellhilfe, Systemprüfung, Datensicherung und Diagnosepaket"
        };
        button.Click += (_, _) => OpenSupportCenter();
        parent.Children.Add(button);
    }

    private void OpenSupportCenter()
    {
        if (_supportWindow is null || !_supportWindow.IsLoaded)
        {
            _supportWindow = new SupportCenterWindow(_window);
            _supportWindow.Closed += (_, _) => _supportWindow = null;
            _supportWindow.Show();
        }
        else
        {
            _supportWindow.Show();
            _supportWindow.Activate();
        }

        VersionUiService.NormalizeWindow(_supportWindow);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_supportWindow is not null)
            _supportWindow.Close();
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        Instances.Remove(_window);
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
