using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Partcounter.Services;

/// <summary>
/// Stellt die produktionsrelevante Betriebsart-Umschaltung dauerhaft sichtbar bereit.
/// Simulation/Echtbetrieb ist eine Bedienfunktion; administrative Konfigurationen
/// wie LOGO!/Modbus, Drucker und Systemeinstellungen bleiben weiterhin geschützt.
/// </summary>
public static class OperatingModeUiBootstrap
{
    public const string ToggleAutomationId = "PartcounterOperatingModeToggle";

    private static readonly ConditionalWeakTable<MainWindow, Button> ToggleButtons = new();

    public static void Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Der reale Mindestwert muss zum bereits vorhandenen 800x500-Layoutgate passen.
        window.MinWidth = 800;
        window.MinHeight = 500;

        if (ToggleButtons.TryGetValue(window, out _))
            return;

        if (window.Content is not DockPanel root)
            throw new InvalidOperationException("MainWindow root must be a DockPanel for the operating-mode bar.");

        // Der alte Kopfzeilen-Schalter wird später vom historischen Admin-Code gefunden.
        // Er bleibt technisch erhalten, wird aber ausgeblendet, damit es nur einen eindeutigen
        // frei bedienbaren Umschalter gibt.
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
    }

    internal static Button? GetPrimaryToggle(MainWindow window) =>
        ToggleButtons.TryGetValue(window, out var button) ? button : null;

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
            Text = "Simulation ↔ Echtbetrieb",
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
        status.SetBinding(TextBlock.TextProperty, new Binding("SystemStatusText"));
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
            ToolTip = "Betriebsart bewusst umschalten. HF4 hält den Echtbetrieb nach aufgebauten Modbus-Sessions eingerastet; einzelne Offline-/Recovery-Fehler schalten nicht mehr die gesamte Anwendung zurück."
        };
        AutomationProperties.SetAutomationId(toggleButton, ToggleAutomationId);

        // Der historische Admin-Code sucht gezielt nach einem Button, dessen Content selbst
        // an OperatingModeButtonText gebunden ist. Beim Produktionsschalter ist nur der innere
        // Text gebunden; der Button selbst bleibt damit frei von der Admin-Abfanglogik.
        var toggleText = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        toggleText.SetBinding(TextBlock.TextProperty, new Binding("OperatingModeButtonText"));
        toggleButton.Content = toggleText;
        toggleButton.SetBinding(Button.CommandProperty, new Binding("Hf4ToggleOperatingModeCommand"));

        var buttonStyle = new Style(typeof(Button));
        buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(46, 125, 50))));
        buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(35, 96, 39))));

        var liveTrigger = new DataTrigger
        {
            Binding = new Binding("IsSimulationMode"),
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
                        "OperatingModeButtonText",
                        StringComparison.Ordinal))
                    return button;
            }

            var nested = FindBoundOperatingModeButton(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
