using System.Windows;
using System.Windows.Controls;
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
            // The stress gate validates the production view model and must never be
            // slowed or blocked by a commissioning-only modal dialog. Layout mode still
            // attaches the view so that its visual tree remains part of layout coverage.
            if ((Application.Current as App)?.IsStressMode == true)
                return;

            if (_window.DataContext is not MainViewModel main)
                return;

            // MainWindow creates the commissioning view asynchronously. The admin hub
            // can move its tab into a nested TabControl at any time, so attachment uses
            // MainWindow's stable view reference rather than the visual tab hierarchy.
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (TryAttachToCommissioning(main))
                    return;

                await Task.Delay(150);
            }

            if ((Application.Current as App)?.IsAutomatedValidationMode != true)
            {
                MessageBox.Show(
                    "Der Bereich 'Inbetriebnahme / Diagnose' wurde nicht gefunden. Die übrige Anwendung bleibt verfügbar.",
                    AppVersionInfo.ProductTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            if ((Application.Current as App)?.IsAutomatedValidationMode != true)
            {
                MessageBox.Show(
                    $"Die Live-Abnahme konnte nicht initialisiert werden.\n\n{ex.Message}",
                    AppVersionInfo.ProductTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private bool TryAttachToCommissioning(MainViewModel main)
    {
        var commissioningView = _window.CommissioningView;
        if (commissioningView is null)
            return false;

        var innerTabs = commissioningView.CommissioningTabs;

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

    private static async Task InitializeViewModelAsync(LiveCommissioningViewModel viewModel)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            if ((Application.Current as App)?.IsAutomatedValidationMode != true)
            {
                MessageBox.Show(
                    $"Live-Abnahme konnte nicht initialisiert werden.\n\n{ex.Message}",
                    AppVersionInfo.ProductTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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

}
