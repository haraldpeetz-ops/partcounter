using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed record DocumentationCaptureResult(
    string ScreenshotDirectory,
    string ZipPath,
    int CapturedCount,
    IReadOnlyList<string> CapturedFiles,
    IReadOnlyList<string> SkippedItems);

/// <summary>
/// R001.20: Erstellt Original-Screenshots direkt aus der laufenden WPF-Anwendung.
/// Es werden keine echten Fehler provoziert, keine Modbus-Schreibbefehle gesendet und keine
/// geschützten Bereiche automatisch entsperrt. Sichtbare/verfügbare Oberflächen werden gerendert.
/// Sensible gebundene Werte werden ausschließlich für den Renderzeitpunkt ausgeblendet.
/// </summary>
public sealed class DocumentationCaptureService
{
    private static readonly string[] SensitiveBindingTerms =
    [
        "OrderNumber", "ArticleNumber", "ArticleDescription", "ToolNumber", "MachineName",
        "IpAddress", "FilePath", "ArchiveFolder", "ErrorFolder", "RestUrl", "Username",
        "Password", "BearerToken", "ApiKeyValue", "ClientCertificatePath", "ClientCertificatePassword",
        "AdditionalHeaders", "RequestBody", "LabelPrinterName", "MachineAliasMap", "DisplayName"
    ];

    public static string ScreenshotDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Partcounter",
        "HelpScreenshots");

    public static string PackageDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Partcounter",
        "DocumentationPackages");

    private readonly List<string> _captured = new();
    private readonly List<string> _skipped = new();

    public async Task<DocumentationCaptureResult> CapturePriorityAAsync(
        MainWindow window,
        HelpCenterWindow? helpWindow = null,
        Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        Directory.CreateDirectory(ScreenshotDirectory);
        Directory.CreateDirectory(PackageDirectory);

        _captured.Clear();
        _skipped.Clear();

        var originalTab = (window.FindName("MainTabs") as TabControl)?.SelectedItem;
        var originalWindowState = window.WindowState;
        var originalWidth = window.Width;
        var originalHeight = window.Height;

        try
        {
            window.WindowState = WindowState.Normal;
            window.Width = 1680;
            window.Height = 980;
            await StabilizeAsync(window);

            if (helpWindow is not null && helpWindow.IsLoaded)
            {
                progress?.Invoke("Hilfezentrum wird aufgenommen …");
                await StabilizeAsync(helpWindow);
                CaptureElement(helpWindow, "00_hilfezentrum.png");
            }

            var tabs = window.FindName("MainTabs") as TabControl;
            if (tabs is null)
                throw new InvalidOperationException("MainTabs wurde im Hauptfenster nicht gefunden.");

            await CaptureMainTabAsync(window, tabs, "Leitstand", "03_hauptnavigation.png", progress);
            await CaptureMainTabAsync(window, tabs, "Leitstand", "10_leitstand_uebersicht.png", progress);
            await CaptureMainTabAsync(window, tabs, "Leitstand", "11_auftrag_starten.png", progress);
            await CaptureMainTabAsync(window, tabs, "Artikelstamm", "20_artikelstamm.png", progress);
            await CaptureMainTabAsync(window, tabs, "VE-Historie", "30_ve_historie.png", progress);

            await CaptureReprintDialogAsync(window, progress);

            await CaptureMainTabAsync(window, tabs, "Maschinen / Modbus", "40_maschinen_modbus.png", progress);
            await CaptureMainTabAsync(window, tabs, "Etiketteneditor", "50_etiketteneditor_gesamt.png", progress);
            await CaptureMainTabAsync(window, tabs, "Etiketteneditor", "52_etiketteneditor_bild.png", progress);
            await CaptureMainTabAsync(window, tabs, "Inbetriebnahme / Diagnose", "60_inbetriebnahme_gesamt.png", progress);
            await CaptureNestedTabAsync(window, tabs, "Inbetriebnahme / Diagnose", "Live-Abnahme", "61_live_abnahme.png", progress);
            await CaptureMainTabAsync(window, tabs, "Rolloutstatus", "62_rollout_30.png", progress);

            await CaptureMainTabAsync(window, tabs, "ARBURG ALS", "70_als_auftraege.png", progress);
            await CaptureNestedTabAsync(window, tabs, "ARBURG ALS", "Verbindung / Quelle", "71_als_verbindung.png", progress, strictRedaction: true);
            await CaptureNestedTabAsync(window, tabs, "ARBURG ALS", "Feldmapping", "72_als_feldmapping.png", progress);
            await CaptureAlsDiagnosticAsync(window, tabs, progress);

            await CaptureMainTabAsync(window, tabs, "Einstellungen / Druck", "80_druckeinstellungen.png", progress, strictRedaction: true);
            await CaptureMainTabAsync(window, tabs, "Einstellungen / Druck", "82_updatecenter.png", progress, strictRedaction: true);
            await CaptureMainTabAsync(window, tabs, "Einstellungen / Druck", "83_backup_diagnose.png", progress, strictRedaction: true);

            if (SelectMainTab(tabs, "VE-Historie"))
            {
                await StabilizeAsync(window);
                CaptureElement(window, "32_reprint_snapshot_status.png");
            }
            else
            {
                _skipped.Add("32_reprint_snapshot_status.png – VE-Historie nicht verfügbar");
            }

            progress?.Invoke("Screenshot-Paket wird erstellt …");
            var zipPath = BuildZipPath();
            WriteManifest(zipPath);
            CreateZipPackage(zipPath);

            return new DocumentationCaptureResult(
                ScreenshotDirectory,
                zipPath,
                _captured.Count,
                _captured.ToList(),
                _skipped.ToList());
        }
        finally
        {
            if (window.FindName("MainTabs") is TabControl mainTabs && originalTab is not null)
                mainTabs.SelectedItem = originalTab;

            window.Width = originalWidth;
            window.Height = originalHeight;
            window.WindowState = originalWindowState;
            await StabilizeAsync(window);
        }
    }

    private async Task CaptureMainTabAsync(
        MainWindow window,
        TabControl tabs,
        string headerContains,
        string fileName,
        Action<string>? progress,
        bool strictRedaction = false)
    {
        progress?.Invoke($"{fileName} wird erstellt …");
        if (!SelectMainTab(tabs, headerContains))
        {
            _skipped.Add($"{fileName} – Reiter '{headerContains}' nicht verfügbar oder nicht freigegeben");
            return;
        }

        await StabilizeAsync(window);
        CaptureElement(window, fileName, strictRedaction);
    }

    private async Task CaptureNestedTabAsync(
        MainWindow window,
        TabControl mainTabs,
        string mainHeader,
        string nestedHeader,
        string fileName,
        Action<string>? progress,
        bool strictRedaction = false)
    {
        progress?.Invoke($"{fileName} wird erstellt …");
        if (!SelectMainTab(mainTabs, mainHeader))
        {
            _skipped.Add($"{fileName} – Hauptreiter '{mainHeader}' nicht verfügbar");
            return;
        }

        await StabilizeAsync(window);
        var selected = mainTabs.SelectedItem as TabItem;
        var nested = selected is null
            ? null
            : FindDescendant<TabControl>(selected, control => control.Items.OfType<TabItem>()
                .Any(tab => HeaderContains(tab, nestedHeader)));

        if (nested is null)
        {
            _skipped.Add($"{fileName} – Unterreiter '{nestedHeader}' nicht verfügbar");
            return;
        }

        var target = nested.Items.OfType<TabItem>().FirstOrDefault(tab => HeaderContains(tab, nestedHeader));
        if (target is null || !target.IsEnabled)
        {
            _skipped.Add($"{fileName} – Unterreiter '{nestedHeader}' ist gesperrt");
            return;
        }

        nested.SelectedItem = target;
        await StabilizeAsync(window);
        CaptureElement(window, fileName, strictRedaction);
    }

    private async Task CaptureReprintDialogAsync(MainWindow window, Action<string>? progress)
    {
        progress?.Invoke("31_reprint_dialog.png wird erstellt …");
        try
        {
            var record = new PackagingUnitRecord(
                "DOC-DEMO-M01-VE0012",
                1,
                "Spritzgussmaschine 01",
                12,
                "DOC-AUF-20260829-001",
                "DEMO-1000",
                "Demoartikel für Dokumentation",
                "WZ-DEMO-08",
                8,
                1000,
                1000,
                0,
                VeCompletionReason.AutomaticFull,
                DateTime.UtcNow.AddMinutes(-12),
                "Printed",
                DateTime.UtcNow.AddMinutes(-12));

            var dialog = new LabelReprintDialog(record, 1)
            {
                Owner = window,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = window.Left + 160,
                Top = window.Top + 100,
                ShowInTaskbar = false
            };

            dialog.Show();
            await StabilizeAsync(dialog);
            CaptureElement(dialog, "31_reprint_dialog.png");
            dialog.Close();
        }
        catch (Exception ex)
        {
            _skipped.Add($"31_reprint_dialog.png – {ex.Message}");
        }
    }

    private async Task CaptureAlsDiagnosticAsync(MainWindow window, TabControl mainTabs, Action<string>? progress)
    {
        progress?.Invoke("74_als_fehlerdiagnose.png wird erstellt …");
        if (!SelectMainTab(mainTabs, "ARBURG ALS"))
        {
            _skipped.Add("74_als_fehlerdiagnose.png – ARBURG ALS nicht verfügbar");
            return;
        }

        await StabilizeAsync(window);
        var selected = mainTabs.SelectedItem as TabItem;
        var statusText = selected is null
            ? null
            : FindDescendant<TextBlock>(selected, block =>
            {
                var expression = BindingOperations.GetBindingExpression(block, TextBlock.TextProperty);
                return string.Equals(expression?.ParentBinding.Path?.Path, "StatusText", StringComparison.Ordinal);
            });

        if (statusText is null)
        {
            _skipped.Add("74_als_fehlerdiagnose.png – ALS-Statusanzeige nicht gefunden");
            return;
        }

        var binding = BindingOperations.GetBindingBase(statusText, TextBlock.TextProperty);
        var originalForeground = statusText.Foreground;
        try
        {
            BindingOperations.ClearBinding(statusText, TextBlock.TextProperty);
            statusText.Text = "Dokumentationstest: ALS-Quelle nicht erreichbar – Datei/Verzeichnis nicht gefunden. Bitte Pfad und Berechtigungen prüfen.";
            statusText.Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0x4A, 0x00));
            await StabilizeAsync(window);
            CaptureElement(window, "74_als_fehlerdiagnose.png", strictRedaction: true);
        }
        finally
        {
            statusText.Foreground = originalForeground;
            if (binding is not null)
                BindingOperations.SetBinding(statusText, TextBlock.TextProperty, binding);
        }
    }

    private static bool SelectMainTab(TabControl tabs, string headerContains)
    {
        var target = tabs.Items.OfType<TabItem>().FirstOrDefault(tab => HeaderContains(tab, headerContains));
        if (target is null || !target.IsEnabled)
            return false;

        tabs.SelectedItem = target;
        return ReferenceEquals(tabs.SelectedItem, target);
    }

    private static bool HeaderContains(TabItem tab, string value) =>
        tab.Header?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private void CaptureElement(FrameworkElement element, string fileName, bool strictRedaction = false)
    {
        var redacted = ApplyRedactions(element, strictRedaction);
        try
        {
            element.UpdateLayout();
            var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(ScreenshotDirectory, fileName);
            using var stream = File.Create(path);
            encoder.Save(stream);
            _captured.Add(fileName);
        }
        finally
        {
            foreach (var item in redacted)
                item.Element.Opacity = item.OriginalOpacity;
        }
    }

    private static List<RedactedElement> ApplyRedactions(FrameworkElement root, bool strictRedaction)
    {
        var result = new List<RedactedElement>();
        foreach (var element in FindDescendants<FrameworkElement>(root))
        {
            var hide = element switch
            {
                TextBox textBox => strictRedaction || IsSensitiveBinding(textBox, TextBox.TextProperty),
                PasswordBox => true,
                ComboBox combo => strictRedaction || IsSensitiveBinding(combo, ComboBox.SelectedItemProperty) || IsSensitiveBinding(combo, ComboBox.TextProperty),
                TextBlock text => IsSensitiveBinding(text, TextBlock.TextProperty),
                _ => false
            };

            if (!hide || element.Opacity <= 0)
                continue;

            result.Add(new RedactedElement(element, element.Opacity));
            element.Opacity = 0;
        }
        return result;
    }

    private static bool IsSensitiveBinding(DependencyObject element, DependencyProperty property)
    {
        var expression = BindingOperations.GetBindingExpression(element, property);
        var path = expression?.ParentBinding.Path?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return SensitiveBindingTerms.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildZipPath() => Path.Combine(
        PackageDirectory,
        $"Partcounter_R00120_HelpScreenshots_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

    private static void CreateZipPackage(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        ZipFile.CreateFromDirectory(ScreenshotDirectory, path, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    private void WriteManifest(string zipPath)
    {
        var path = Path.Combine(ScreenshotDirectory, "CAPTURE_MANIFEST.txt");
        var lines = new List<string>
        {
            "PARTCOUNTER R001.20 – AUTOMATISCHE DOKUMENTATIONSAUFNAHME",
            $"Erstellt: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
            $"ZIP: {zipPath}",
            $"Erfolgreich: {_captured.Count}",
            string.Empty,
            "ERSTELLTE DATEIEN:"
        };
        lines.AddRange(_captured.OrderBy(x => x).Select(x => $"- {x}"));
        lines.Add(string.Empty);
        lines.Add("AUSGELASSEN / NICHT VERFÜGBAR:");
        lines.AddRange(_skipped.Count == 0 ? new[] { "- keine" } : _skipped.Select(x => $"- {x}"));
        lines.Add(string.Empty);
        lines.Add("DATENSCHUTZ: Sensible gebundene Werte wie Auftrags-/Artikelnummern, IP-Adressen, ALS-Zugangsdaten, Pfade und Druckernamen werden während des Renderns automatisch ausgeblendet. Die Datenquellen werden nicht verändert.");
        lines.Add("SICHERHEIT: Die Automatik entsperrt keine geschützten Reiter, provoziert keine realen ALS-/Netzwerkfehler und sendet keine Modbus-Schreibbefehle. Der ALS-Fehler-Screenshot verwendet ausschließlich einen temporären UI-Dokumentationshinweis.");
        File.WriteAllLines(path, lines);
    }

    private static async Task StabilizeAsync(FrameworkElement element)
    {
        element.UpdateLayout();
        await element.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await Task.Delay(120);
        element.UpdateLayout();
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

    private sealed record RedactedElement(FrameworkElement Element, double OriginalOpacity);
}
