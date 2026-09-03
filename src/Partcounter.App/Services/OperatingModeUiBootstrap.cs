using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;

namespace Partcounter.Services;

/// <summary>
/// Stellt die produktionsrelevante Betriebsart-Umschaltung dauerhaft sichtbar bereit,
/// initialisiert die HF5-Isolation und schützt die aktuelle Versions-/Modusanzeige vor
/// historischen Bootstrap-Schichten.
/// </summary>
public static class OperatingModeUiBootstrap
{
    public const string ToggleAutomationId = "PartcounterOperatingModeToggle";

    private static readonly ConditionalWeakTable<MainWindow, Button> ToggleButtons = new();

    public static void Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.MinWidth = 800;
        window.MinHeight = 500;

        if (ToggleButtons.TryGetValue(window, out _))
            return;

        if (window.Content is not DockPanel root)
            throw new InvalidOperationException("MainWindow root must be a DockPanel for the operating-mode bar.");

        var legacyHeaderToggle = FindBoundOperatingModeButton(root);
        if (legacyHeaderToggle is not null)
        {
            legacyHeaderToggle.Visibility = Visibility.Collapsed;
            legacyHeaderToggle.IsTabStop = false;
            legacyHeaderToggle.Focusable = false;
        }

        var mainTabs = root.Children
            .OfType<TabControl>()
            .FirstOrDefault(tab => string.Equals(tab.Name, "MainTabs", StringComparison.Ordinal));
        if (mainTabs is null)
            throw new InvalidOperationException("MainTabs not found while creating the operating-mode bar.");

        var border = BuildBar(out var toggleButton);
        DockPanel.SetDock(border, Dock.Top);

        var tabIndex = root.Children.IndexOf(mainTabs);
        root.Children.Insert(tabIndex, border);
        ToggleButtons.Add(window, toggleButton);

        window.Loaded += OnWindowLoaded;
    }

    internal static Button? GetPrimaryToggle(MainWindow window) =>
        ToggleButtons.TryGetValue(window, out var button) ? button : null;

    private static async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        try
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                if (window.DataContext is MainViewModel vm &&
                    vm.Machines.Count > 0 &&
                    vm.Articles.Count > 0 &&
                    vm.SelectedMachine is not null)
                {
                    // MainWindow-Basisinitialisierung einschließlich dynamischer Reiter/Event-Hooks
                    // zuerst abschließen lassen. Danach atomarer Wechsel auf die HF5-Simulationsdomäne.
                    await Task.Delay(150);
                    await vm.EnableHf5IsolationAsync();
                    AttachUiIntegrityGuard(window, vm);
                    NormalizeCurrentRevisionUi(window, vm);
                    return;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("HF5-Betriebsartenisolation konnte nicht initialisiert werden, weil die Stammdaten nicht rechtzeitig verfügbar waren.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Die HF5-Betriebsartenisolation konnte nicht initialisiert werden.\n\n{ex.Message}",
                AppVersionInfo.ProductTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void AttachUiIntegrityGuard(MainWindow window, MainViewModel vm)
    {
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName is not (
                nameof(MainViewModel.IsSimulationMode) or
                nameof(MainViewModel.SystemStatusText) or
                nameof(MainViewModel.Hf5IsUsingSimulationMachines) or
                nameof(MainViewModel.Hf5IsUsingLiveMachines)))
                return;

            // Historische Module stellen teilweise bei ApplicationIdle alte Revisionstexte wieder her.
            // SystemIdle läuft danach und macht die aktuelle Assembly-Version zur letzten Autorität.
            _ = window.Dispatcher.BeginInvoke(
                DispatcherPriority.SystemIdle,
                new Action(() => NormalizeCurrentRevisionUi(window, vm)));
        };

        vm.PropertyChanged += handler;
        window.Closed += (_, _) =>
        {
            if (handler is not null)
                vm.PropertyChanged -= handler;
        };
    }

    private static void NormalizeCurrentRevisionUi(MainWindow window, MainViewModel vm)
    {
        window.Title = AppVersionInfo.ProductTitle;
        var currentStatus = vm.SystemStatusText;

        foreach (var text in FindDescendants<TextBlock>(window))
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            var boundToStatus = expression?.ParentBinding.Path?.Path == nameof(MainViewModel.SystemStatusText);
            var looksLikeLegacyModeStatus =
                text.Text?.StartsWith("R001.", StringComparison.Ordinal) == true &&
                (text.Text.Contains("SIMULATION", StringComparison.OrdinalIgnoreCase) ||
                 text.Text.Contains("ECHTBETRIEB", StringComparison.OrdinalIgnoreCase));

            if (boundToStatus || looksLikeLegacyModeStatus)
            {
                // Eine von Altcode entfernte Bindung wird absichtlich nicht benötigt: dieser Guard
                // aktualisiert den Status bei jedem HF5-Modusereignis deterministisch.
                BindingOperations.ClearBinding(text, TextBlock.TextProperty);
                text.Text = currentStatus;
                continue;
            }

            if (text.Text?.StartsWith("Installiert:", StringComparison.OrdinalIgnoreCase) == true)
                text.Text = AppVersionInfo.InstalledText;
        }
    }

    private static Border BuildBar(out Button toggleButton)
    {
        var border = new Border
        {
            Margin = new Thickness(8, 8, 8, 0),
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var caption = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        caption.Children.Add(new TextBlock
        {
            Text = "BETRIEBSART",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 33, 43))
        });
        caption.Children.Add(new TextBlock
        {
            Text = "HF5 · getrennte Zustände: Simulation ↔ Echtbetrieb",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(86, 100, 115))
        });
        grid.Children.Add(caption);

        var status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 14, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(45, 55, 72))
        };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.SystemStatusText)));
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);

        toggleButton = new Button
        {
            MinWidth = 220,
            MinHeight = 40,
            Padding = new Thickness(18, 8, 18, 8),
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "HF5: Simulation und Echtbetrieb besitzen getrennte Maschinen-, Auftrags- und Eingabezustände. Ein Wechsel vermischt keine Zähler, Recovery-Daten oder Modbus-Snapshots."
        };
        AutomationProperties.SetAutomationId(toggleButton, ToggleAutomationId);

        // Der historische Admin-Code sucht nach einer direkten Button.Content-Bindung an
        // OperatingModeButtonText. Beim Produktionsschalter ist nur der innere Text gebunden.
        var toggleText = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        toggleText.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.OperatingModeButtonText)));
        toggleButton.Content = toggleText;
        toggleButton.SetBinding(Button.CommandProperty, new Binding(nameof(MainViewModel.Hf5ToggleOperatingModeCommand)));

        var buttonStyle = new Style(typeof(Button));
        buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(46, 125, 50))));
        buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(35, 96, 39))));

        var liveTrigger = new DataTrigger
        {
            Binding = new Binding(nameof(MainViewModel.IsSimulationMode)),
            Value = false
        };
        liveTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(180, 83, 9))));
        liveTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(146, 64, 14))));
        buttonStyle.Triggers.Add(liveTrigger);
        toggleButton.Style = buttonStyle;

        Grid.SetColumn(toggleButton, 2);
        grid.Children.Add(toggleButton);

        border.Child = grid;
        return border;
    }

    private static Button? FindBoundOperatingModeButton(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button)
            {
                var expression = BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty);
                if (string.Equals(
                        expression?.ParentBinding.Path?.Path,
                        nameof(MainViewModel.OperatingModeButtonText),
                        StringComparison.Ordinal))
                    return button;
            }

            var nested = FindBoundOperatingModeButton(child);
            if (nested is not null)
                return nested;
        }

        return null;
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
