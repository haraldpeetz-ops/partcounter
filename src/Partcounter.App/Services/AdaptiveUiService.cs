using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Partcounter.Services;

/// <summary>
/// Zentrale, nicht fachlogische UI-Härtung für kleine Displays und hohe DPI-Skalierung.
/// Die Service-Schicht verändert keine Datenbindungen oder Produktionslogik. Sie begrenzt Fenster
/// auf die verfügbare Arbeitsfläche, neutralisiert zu große Mindestmaße und setzt konservative
/// Layout-Breakpoints für kritische WPF-Container.
/// </summary>
public static class AdaptiveUiService
{
    private sealed class AdaptiveWindowState
    {
        public bool ValidationOverride { get; set; }
        public double ValidationWidth { get; set; }
        public double ValidationHeight { get; set; }
    }

    private static readonly Dictionary<Window, AdaptiveWindowState> Windows = new();
    private static readonly Dictionary<ColumnDefinition, GridLength> OriginalColumns = new();
    private static readonly Dictionary<FrameworkElement, double> OriginalExplicitWidths = new();
    private static bool _initialized;

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
        if (window is null || !window.IsLoaded)
            return;

        if (!Windows.TryGetValue(window, out var state))
        {
            state = new AdaptiveWindowState();
            Windows[window] = state;
        }

        if (!state.ValidationOverride)
            FitWindowToWorkingArea(window);

        var viewportWidth = state.ValidationOverride
            ? state.ValidationWidth
            : Math.Max(1, window.ActualWidth);
        var viewportHeight = state.ValidationOverride
            ? state.ValidationHeight
            : Math.Max(1, window.ActualHeight);

        NormalizeVisualTree(window, viewportWidth, viewportHeight);
        if (window is MainWindow main)
            ApplyMainWindowBreakpoints(main, viewportWidth, viewportHeight);
    }

    public static void ApplyValidationViewport(Window window, double width, double height)
    {
        if (!Windows.TryGetValue(window, out var state))
        {
            state = new AdaptiveWindowState();
            Windows[window] = state;
        }

        state.ValidationOverride = true;
        state.ValidationWidth = Math.Max(640, width);
        state.ValidationHeight = Math.Max(420, height);

        window.MinWidth = 0;
        window.MinHeight = 0;
        window.MaxWidth = double.PositiveInfinity;
        window.MaxHeight = double.PositiveInfinity;
        window.Width = state.ValidationWidth;
        window.Height = state.ValidationHeight;
        window.UpdateLayout();
        NormalizeWindow(window);
        window.UpdateLayout();
    }

    public static void EndValidationViewport(Window window)
    {
        if (Windows.TryGetValue(window, out var state))
            state.ValidationOverride = false;
        NormalizeWindow(window);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (!Windows.ContainsKey(window))
        {
            Windows[window] = new AdaptiveWindowState();
            window.SizeChanged += OnWindowSizeChanged;
            window.StateChanged += OnWindowStateChanged;
            window.Closed += OnWindowClosed;
        }

        FitWindowToWorkingArea(window);
        ScheduleNormalize(window);
    }

    private static void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Window window)
            ScheduleNormalize(window);
    }

    private static void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
            ScheduleNormalize(window);
    }

    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        window.SizeChanged -= OnWindowSizeChanged;
        window.StateChanged -= OnWindowStateChanged;
        window.Closed -= OnWindowClosed;
        Windows.Remove(window);
    }

    private static void ScheduleNormalize(Window window)
    {
        if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished)
            return;

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => NormalizeWindow(window)));
    }

    private static void FitWindowToWorkingArea(Window window)
    {
        var area = SystemParameters.WorkArea;
        var availableWidth = Math.Max(640, area.Width - 12);
        var availableHeight = Math.Max(420, area.Height - 12);

        var safeMinimumWidth = Math.Min(880, availableWidth);
        var safeMinimumHeight = Math.Min(560, availableHeight);
        if (window.MinWidth > safeMinimumWidth)
            window.MinWidth = safeMinimumWidth;
        if (window.MinHeight > safeMinimumHeight)
            window.MinHeight = safeMinimumHeight;

        window.MaxWidth = availableWidth;
        window.MaxHeight = availableHeight;

        if (window.WindowState == System.Windows.WindowState.Maximized)
            return;

        var desiredWidth = double.IsNaN(window.Width) || window.Width <= 0
            ? Math.Min(availableWidth, Math.Max(800, window.ActualWidth))
            : Math.Min(window.Width, availableWidth);
        var desiredHeight = double.IsNaN(window.Height) || window.Height <= 0
            ? Math.Min(availableHeight, Math.Max(520, window.ActualHeight))
            : Math.Min(window.Height, availableHeight);

        if (window is MainWindow)
        {
            if (availableWidth < 1500)
                desiredWidth = availableWidth;
            if (availableHeight < 900)
                desiredHeight = availableHeight;
        }

        window.Width = Math.Max(Math.Min(window.MinWidth, availableWidth), desiredWidth);
        window.Height = Math.Max(Math.Min(window.MinHeight, availableHeight), desiredHeight);

        if (double.IsNaN(window.Left) || window.Left < area.Left || window.Left + window.Width > area.Right)
            window.Left = area.Left + Math.Max(0, (area.Width - window.Width) / 2);
        if (double.IsNaN(window.Top) || window.Top < area.Top || window.Top + window.Height > area.Bottom)
            window.Top = area.Top + Math.Max(0, (area.Height - window.Height) / 2);
    }

    private static void NormalizeVisualTree(DependencyObject root, double viewportWidth, double viewportHeight)
    {
        var safeWidth = Math.Max(320, viewportWidth - 36);
        var safeHeight = Math.Max(260, viewportHeight - 90);

        if (root is FrameworkElement element)
        {
            if (element is UserControl or TabControl or Grid or ScrollViewer or ItemsControl)
            {
                if (element.MinWidth > safeWidth)
                    element.MinWidth = 0;
                if (element.MinHeight > safeHeight)
                    element.MinHeight = 0;
            }

            if (element is StackPanel or Border)
            {
                if (!double.IsNaN(element.Width) && element.Width > safeWidth)
                {
                    OriginalExplicitWidths.TryAdd(element, element.Width);
                    element.Width = double.NaN;
                    element.MaxWidth = safeWidth;
                }
                else if (OriginalExplicitWidths.TryGetValue(element, out var original) && safeWidth >= original + 24)
                {
                    element.Width = original;
                    element.MaxWidth = double.PositiveInfinity;
                    OriginalExplicitWidths.Remove(element);
                }
            }
        }

        if (root is DataGrid grid)
        {
            grid.MinWidth = 0;
            grid.MinHeight = Math.Min(Math.Max(120, grid.MinHeight), safeHeight);
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(grid, true);
        }

        if (root is ScrollViewer viewer)
        {
            viewer.PanningMode = PanningMode.Both;
            viewer.PanningDeceleration = 0.001;
        }

        if (root is Grid layoutGrid)
            NormalizeLargeFixedColumns(layoutGrid, safeWidth);

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            NormalizeVisualTree(VisualTreeHelper.GetChild(root, i), viewportWidth, viewportHeight);
    }

    private static void NormalizeLargeFixedColumns(Grid grid, double safeWidth)
    {
        if (grid.ColumnDefinitions.Count < 2 || grid.ActualWidth <= 0)
            return;

        var actualWidth = Math.Min(safeWidth, grid.ActualWidth);
        if (actualWidth <= 0)
            return;

        var largeFixed = grid.ColumnDefinitions
            .Where(c => c.Width.IsAbsolute && c.Width.Value >= 180)
            .ToList();
        var hasFlexible = grid.ColumnDefinitions.Any(c => c.Width.IsStar || c.Width.IsAuto);
        if (largeFixed.Count == 0 || !hasFlexible)
            return;

        foreach (var column in largeFixed)
            OriginalColumns.TryAdd(column, column.Width);

        var originalFixed = largeFixed.Sum(c => OriginalColumns[c].Value);
        var compact = originalFixed > actualWidth * 0.58;

        foreach (var column in largeFixed)
        {
            var original = OriginalColumns[column];
            if (!compact)
            {
                column.Width = original;
                continue;
            }

            var share = originalFixed <= 0 ? 1d / largeFixed.Count : original.Value / originalFixed;
            var budget = Math.Max(280, actualWidth * 0.50);
            var compactWidth = Math.Max(140, Math.Min(original.Value, budget * share));
            column.Width = new GridLength(compactWidth, GridUnitType.Pixel);
        }
    }

    private static void ApplyMainWindowBreakpoints(MainWindow window, double viewportWidth, double viewportHeight)
    {
        var compact = viewportWidth < 1180 || viewportHeight < 700;
        var veryCompact = viewportWidth < 920 || viewportHeight < 560;

        if (window.FindName("MainTabs") is TabControl mainTabs)
            mainTabs.Margin = compact ? new Thickness(4) : new Thickness(8);

        foreach (var uniformGrid in FindDescendants<UniformGrid>(window))
        {
            if (uniformGrid.Columns < 4)
                continue;

            uniformGrid.Columns = viewportWidth switch
            {
                < 850 => 1,
                < 1080 => 2,
                < 1360 => 3,
                < 1650 => 4,
                _ => 5
            };
        }

        foreach (var panel in FindDescendants<StackPanel>(window))
        {
            if (panel.Parent is Grid parent && Grid.GetColumn(panel) == 1 && parent.ColumnDefinitions.Count == 2)
            {
                panel.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
                panel.HorizontalAlignment = HorizontalAlignment.Right;
                panel.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        foreach (var text in FindDescendants<TextBlock>(window))
        {
            if (text.Text?.StartsWith("30-Maschinen VE-Leitstand", StringComparison.Ordinal) == true)
            {
                text.TextWrapping = TextWrapping.Wrap;
                text.MaxWidth = veryCompact ? Math.Max(320, viewportWidth * 0.55) : 820;
                text.FontSize = compact ? 11 : 13;
            }

            if (string.Equals(text.Text, "PARTCOUNTER", StringComparison.Ordinal))
                text.FontSize = veryCompact ? 21 : compact ? 24 : 28;
        }
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
