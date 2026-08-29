using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;

namespace Partcounter.Services;

/// <summary>
/// Korrigiert sichtbare Versions-/Revisionskennzeichnungen zentral aus AppVersionInfo.
/// Historische Versionsangaben in fachlicher Dokumentation werden bewusst nicht verändert.
/// </summary>
public static partial class VersionUiService
{
    private sealed record ViewModelSubscription(
        INotifyPropertyChanged Notifier,
        PropertyChangedEventHandler Handler);

    private static readonly Dictionary<Window, ViewModelSubscription> Subscriptions = new();
    private static bool _initialized;

    [GeneratedRegex(@"R\d{3}\.\d{1,3}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevisionRegex();

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded),
            handledEventsToo: true);
    }

    public static void NormalizeWindow(Window window)
    {
        if (window is null)
            return;

        if (!string.IsNullOrWhiteSpace(window.Title) && RevisionRegex().IsMatch(window.Title))
            window.Title = RevisionRegex().Replace(window.Title, AppVersionInfo.Revision, 1);
        else if (window is MainWindow)
            window.Title = AppVersionInfo.ProductTitle;

        NormalizeTextBlocks(window, window.GetType().Name);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
            return;

        TrackRuntimeChanges(window);
        ScheduleNormalization(window);
    }

    private static void TrackRuntimeChanges(Window window)
    {
        if (window is not MainWindow || Subscriptions.ContainsKey(window))
            return;

        if (window.DataContext is not INotifyPropertyChanged notifier)
            return;

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
                ScheduleNormalization(window);
        };

        notifier.PropertyChanged += handler;
        Subscriptions[window] = new ViewModelSubscription(notifier, handler);
        window.Closed += OnTrackedWindowClosed;
    }

    private static void OnTrackedWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window || !Subscriptions.Remove(window, out var subscription))
            return;

        subscription.Notifier.PropertyChanged -= subscription.Handler;
        window.Closed -= OnTrackedWindowClosed;
    }

    private static void ScheduleNormalization(Window window)
    {
        if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished)
            return;

        // SystemIdle läuft nach den älteren Loaded-/PropertyChanged-Bootstraps.
        // Damit gewinnt immer die zentrale, aus der Assembly gelesene Versionsquelle.
        window.Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(() => NormalizeWindow(window)));
    }

    private static void NormalizeTextBlocks(DependencyObject root, string windowTypeName)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock textBlock)
                textBlock.Text = NormalizeText(textBlock.Text, windowTypeName);

            NormalizeTextBlocks(child, windowTypeName);
        }
    }

    private static string NormalizeText(string? value, string windowTypeName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var text = value;

        if (text.Contains("· SIMULATION", StringComparison.OrdinalIgnoreCase) && RevisionRegex().IsMatch(text))
            return AppVersionInfo.SimulationStatus;

        if (text.Contains("· ECHTBETRIEB", StringComparison.OrdinalIgnoreCase) && RevisionRegex().IsMatch(text))
            return AppVersionInfo.ProductionStatus;

        if (text.StartsWith("Installiert:", StringComparison.OrdinalIgnoreCase) && RevisionRegex().IsMatch(text))
            return AppVersionInfo.InstalledText;

        if (string.Equals(windowTypeName, "HelpCenterWindow", StringComparison.Ordinal) &&
            text.StartsWith("R", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("· Bedienung", StringComparison.OrdinalIgnoreCase) &&
            RevisionRegex().IsMatch(text))
        {
            return RevisionRegex().Replace(text, AppVersionInfo.Revision, 1);
        }

        if (string.Equals(windowTypeName, "AboutWindow", StringComparison.Ordinal) &&
            RevisionRegex().IsMatch(text) &&
            RevisionRegex().Match(text).Value.Length == text.Trim().Length)
        {
            return AppVersionInfo.Revision;
        }

        return text;
    }
}
