using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.ViewModels;

namespace Partcounter.Services;

public sealed class LayoutValidationService
{
    private static readonly (int Width, int Height)[] Viewports =
    {
        (800, 500),
        (1024, 600),
        (1280, 720),
        (1366, 768),
        (1600, 900),
        (1920, 1080)
    };

    public async Task<int> RunAsync(MainWindow window, string reportPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var issues = new List<string>();
        var notes = new List<string>();

        try
        {
            await WaitUntilAsync(
                () => window.DataContext is MainViewModel vm &&
                      vm.Machines.Count == 30 &&
                      window.AdministrationHubReadyForLayoutValidation,
                TimeSpan.FromSeconds(60),
                cancellationToken);

            foreach (var viewport in Viewports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AdaptiveUiService.ApplyValidationViewport(window, viewport.Width, viewport.Height);
                await YieldLayoutAsync(window, cancellationToken);

                var mainTabs = window.MainTabsForLayoutValidation;
                foreach (var tab in mainTabs.Items.OfType<TabItem>().Where(IsVisibleTab).ToList())
                {
                    window.SelectMainTabForLayoutValidation(tab);
                    await YieldLayoutAsync(window, cancellationToken);
                    AdaptiveUiService.NormalizeWindow(window);
                    await YieldLayoutAsync(window, cancellationToken);

                    ValidateCurrentVisualTree(window, viewport.Width, viewport.Height, TabName(tab), issues);
                    await ExerciseNestedTabsAsync(window, mainTabs, viewport.Width, viewport.Height, issues, cancellationToken);
                }

                notes.Add($"{viewport.Width}x{viewport.Height}: alle sichtbaren Haupt-/Unterreiter durchlaufen");
            }

            AdaptiveUiService.EndValidationViewport(window);
            await WriteReportAsync(reportPath, issues.Count == 0, stopwatch.Elapsed, notes, issues, cancellationToken);
            return issues.Count == 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            issues.Add(ex.ToString());
            try { AdaptiveUiService.EndValidationViewport(window); } catch { }
            await WriteReportAsync(reportPath, false, stopwatch.Elapsed, notes, issues, CancellationToken.None);
            return 3;
        }
    }

    private static async Task ExerciseNestedTabsAsync(
        MainWindow window,
        TabControl mainTabs,
        int viewportWidth,
        int viewportHeight,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        // Mehrere Durchläufe sind nötig, weil das Auswählen eines Tabs weitere verschachtelte
        // TabControls erst in den Visual Tree materialisieren kann.
        for (var pass = 0; pass < 3; pass++)
        {
            var nestedControls = FindDescendants<TabControl>(window)
                .Where(control => !ReferenceEquals(control, mainTabs))
                .Distinct()
                .ToList();

            foreach (var control in nestedControls)
            {
                var items = control.Items.OfType<TabItem>().Where(IsVisibleTab).ToList();
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        control.SelectedItem = item;
                        await YieldLayoutAsync(window, cancellationToken);
                        AdaptiveUiService.NormalizeWindow(window);
                        await YieldLayoutAsync(window, cancellationToken);
                        ValidateCurrentVisualTree(
                            window,
                            viewportWidth,
                            viewportHeight,
                            $"{ControlName(control)} / {TabName(item)}",
                            issues);
                    }
                    catch (Exception ex)
                    {
                        issues.Add($"{viewportWidth}x{viewportHeight} · {TabName(item)}: Tab konnte nicht layoutgeprüft werden: {ex.Message}");
                    }
                }
            }
        }
    }

    private static void ValidateCurrentVisualTree(
        MainWindow window,
        int viewportWidth,
        int viewportHeight,
        string context,
        List<string> issues)
    {
        window.UpdateLayout();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var client = new Rect(0, 0, Math.Max(1, window.ActualWidth), Math.Max(1, window.ActualHeight));

        foreach (var element in FindDescendants<FrameworkElement>(window))
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
                continue;
            if (IsValidationNoise(element))
                continue;

            if (element.MinWidth > viewportWidth + 2 || element.MinHeight > viewportHeight + 2)
            {
                AddUnique(
                    $"{viewportWidth}x{viewportHeight} · {context}: {Describe(element)} erzwingt Min={element.MinWidth:N0}x{element.MinHeight:N0}",
                    issues,
                    seen);
            }

            if (HasScrollableOrClippingAncestor(element, window))
                continue;

            Rect bounds;
            try
            {
                bounds = element.TransformToAncestor(window)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }
            catch
            {
                continue;
            }

            const double tolerance = 6;
            var horizontalOverflow = bounds.Left < client.Left - tolerance || bounds.Right > client.Right + tolerance;
            var verticalOverflow = bounds.Top < client.Top - tolerance || bounds.Bottom > client.Bottom + tolerance;
            if (!horizontalOverflow && !verticalOverflow)
                continue;

            // Sehr kleine dekorative Überschreitungen durch Border/Shadow/FocusChrome interessieren
            // nicht; bedienbare Controls und Inhaltscontainer dagegen schon.
            if (element.ActualWidth < 24 && element.ActualHeight < 24)
                continue;

            AddUnique(
                $"{viewportWidth}x{viewportHeight} · {context}: nicht scrollbar außerhalb des Fensters: {Describe(element)} bounds={bounds.Left:N0},{bounds.Top:N0},{bounds.Width:N0},{bounds.Height:N0}",
                issues,
                seen);
        }
    }

    private static bool HasScrollableOrClippingAncestor(DependencyObject element, Window window)
    {
        DependencyObject? current = element;
        while (current is not null && !ReferenceEquals(current, window))
        {
            if (current is ScrollViewer)
                return true;
            if (current is DataGrid or ListBox or ListView)
                return true;
            if (current is FrameworkElement fe && fe.ClipToBounds && !ReferenceEquals(fe, element))
                return true;

            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool IsValidationNoise(FrameworkElement element) =>
        element is ScrollBar or Thumb or RepeatButton or GridSplitter or Separator or
        element.GetType().Name.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
        element.GetType().Name.Contains("Adorner", StringComparison.OrdinalIgnoreCase) ||
        element.GetType().Name.Contains("Presenter", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisibleTab(TabItem tab) =>
        tab.Visibility == Visibility.Visible &&
        tab.Tag?.ToString()?.StartsWith("PartcounterAdminAlias:", StringComparison.Ordinal) != true;

    private static string TabName(TabItem tab) =>
        tab.Header?.ToString()?.Replace("🔒", string.Empty).Replace("🔓", string.Empty).Trim()
        ?? "Tab";

    private static string ControlName(TabControl control) =>
        string.IsNullOrWhiteSpace(control.Name) ? "Unterreiter" : control.Name;

    private static string Describe(FrameworkElement element)
    {
        var name = string.IsNullOrWhiteSpace(element.Name) ? string.Empty : $"#{element.Name}";
        var text = element switch
        {
            Button button => button.Content?.ToString(),
            TextBlock block => block.Text,
            HeaderedContentControl headered => headered.Header?.ToString(),
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(text) && text.Length > 48)
            text = text[..48] + "…";
        return string.IsNullOrWhiteSpace(text)
            ? $"{element.GetType().Name}{name}"
            : $"{element.GetType().Name}{name} '{text}'";
    }

    private static void AddUnique(string issue, List<string> issues, HashSet<string> seen)
    {
        if (seen.Add(issue))
            issues.Add(issue);
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

    private static async Task YieldLayoutAsync(Window window, CancellationToken cancellationToken)
    {
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.Loaded, cancellationToken);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, cancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
                return;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("Adaptive-UI-Initialisierung hat das Zeitlimit überschritten.");
    }

    private static async Task WriteReportAsync(
        string reportPath,
        bool success,
        TimeSpan elapsed,
        IReadOnlyList<string> notes,
        IReadOnlyList<string> issues,
        CancellationToken cancellationToken)
    {
        reportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var sb = new StringBuilder();
        sb.AppendLine("PARTCOUNTER ADAPTIVE UI / LAYOUT SMOKE TEST");
        sb.AppendLine($"Revision: {AppVersionInfo.Revision}");
        sb.AppendLine($"Version: {AppVersionInfo.VersionText}");
        sb.AppendLine($"Build: {AppVersionInfo.InformationalVersion}");
        sb.AppendLine($"Dauer: {elapsed}");
        sb.AppendLine($"Ergebnis: {(success ? "PASS" : "FAIL")}");
        sb.AppendLine();
        sb.AppendLine("GETESTETE LOGISCHE WPF-VIEWPORTS");
        foreach (var viewport in Viewports)
            sb.AppendLine($"- {viewport.Width} x {viewport.Height}");
        sb.AppendLine();
        sb.AppendLine("NOTIZEN");
        foreach (var note in notes)
            sb.AppendLine($"- {note}");
        sb.AppendLine();
        sb.AppendLine("LAYOUT-PROBLEME");
        if (issues.Count == 0)
            sb.AppendLine("- keine nicht-scrollbaren Überläufe erkannt");
        else
            foreach (var issue in issues.Take(300))
                sb.AppendLine($"- {issue}");
        if (issues.Count > 300)
            sb.AppendLine($"- ... {issues.Count - 300} weitere Probleme gekürzt");

        await File.WriteAllTextAsync(reportPath, sb.ToString(), new UTF8Encoding(false), cancellationToken);
    }
}
