using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter.Services;

/// <summary>
/// Fügt die read-only Live-Abnahme in den vorhandenen, admin-geschützten
/// Inbetriebnahme-/Diagnosebereich ein. HF4 berücksichtigt dabei die von der
/// Admin-Logik vorangestellten Schloss-Symbole und überschreibt keine Versionstexte mehr.
/// </summary>
public sealed class LiveCommissioningBootstrap
{
    private static readonly Dictionary<MainWindow, LiveCommissioningBootstrap> Instances = new();

    private readonly MainWindow _window;
    private LiveCommissioningViewModel? _viewModel;

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

            // MainWindow erzeugt den geschützten Diagnose-Reiter während seines Loaded-Handlers.
            // Danach wird der Header durch die Admin-Logik zu "🔒 Inbetriebnahme / Diagnose"
            // bzw. "🔓 ...". Deshalb bewusst suffix-basiert suchen.
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (TryAttachToCommissioning(main))
                    return;

                await Task.Delay(150);
            }

            MessageBox.Show(
                "Der Bereich 'Inbetriebnahme / Diagnose' wurde nicht gefunden. Die übrige Anwendung bleibt verfügbar.",
                AppVersionInfo.ProductTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Die Live-Abnahme konnte nicht initialisiert werden.\n\n{ex.Message}",
                AppVersionInfo.ProductTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private bool TryAttachToCommissioning(MainViewModel main)
    {
        var mainTabs = FindDescendant<TabControl>(_window, tabs =>
            tabs.Items.OfType<TabItem>().Any(tab => HeaderEndsWith(tab, "Leitstand")));

        var commissioningTab = mainTabs?.Items
            .OfType<TabItem>()
            .FirstOrDefault(tab => HeaderEndsWith(tab, "Inbetriebnahme / Diagnose"));

        if (commissioningTab?.Content is not CommissioningView commissioningView)
            return false;

        var innerTabs = FindDescendant<TabControl>(commissioningView, _ => true);
        if (innerTabs is null)
            return false;

        if (innerTabs.Items.OfType<TabItem>().Any(tab =>
                tab.Header?.ToString()?.StartsWith("Live-Abnahme", StringComparison.Ordinal) == true))
            return true;

        _viewModel = new LiveCommissioningViewModel(main);
        var view = new LiveCommissioningView { DataContext = _viewModel };
        innerTabs.Items.Add(new TabItem
        {
            Header = $"Live-Abnahme {AppVersionInfo.RevisionLabel}",
            Content = view
        });

        _ = InitializeViewModelAsync(_viewModel);
        return true;
    }

    private static bool HeaderEndsWith(TabItem tab, string expected) =>
        tab.Header?.ToString()?.EndsWith(expected, StringComparison.Ordinal) == true;

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
                AppVersionInfo.ProductTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel?.Dispose();
        _viewModel = null;
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
