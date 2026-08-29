using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Partcounter.Services;

namespace Partcounter;

public partial class App : Application
{
    private bool _dispatcherErrorDialogShown;
    private bool _stressMode;
    private bool _layoutMode;
    private string? _stressReportPath;
    private string? _layoutReportPath;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Partcounter");

    private static string LogPath => Path.Combine(LogDirectory, "Partcounter_startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _stressMode = e.Args.Any(arg => string.Equals(arg, "--stress-smoke", StringComparison.OrdinalIgnoreCase));
        _layoutMode = e.Args.Any(arg => string.Equals(arg, "--layout-smoke", StringComparison.OrdinalIgnoreCase));
        _stressReportPath = e.Args
            .FirstOrDefault(arg => arg.StartsWith("--stress-report=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
        _layoutReportPath = e.Args
            .FirstOrDefault(arg => arg.StartsWith("--layout-report=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];

        AdaptiveUiService.Initialize();
        VersionUiService.Initialize();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            WriteLog(
                "START",
                $"{AppVersionInfo.ProductTitle} startup. Version={AppVersionInfo.VersionText}; Build={AppVersionInfo.InformationalVersion}; OS={Environment.OSVersion}; Runtime={Environment.Version}; Base={AppContext.BaseDirectory}; Stress={_stressMode}; Layout={_layoutMode}");

            var window = new MainWindow();
            CompanyBrandingBootstrap.Attach(window);
            InfoUpdateHelpBootstrap.Attach(window);
            ProductionReadinessBootstrap.Attach(window);
            LiveCommissioningBootstrap.Attach(window);
            LabelReprintBootstrap.Attach(window);
            ProfessionalHelpBootstrap.Attach(window);
            SupportCenterBootstrap.Attach(window);
            window.ScheduleAdminHubInitialization();
            OrderSourceHubBootstrap.Attach(window);
            VersionUiService.NormalizeWindow(window);
            MainWindow = window;
            window.Show();
            AdaptiveUiService.NormalizeWindow(window);

            if (_stressMode || _layoutMode)
            {
                window.ShowInTaskbar = false;
                window.Left = -30000;
                window.Top = -30000;
            }

            if (_stressMode)
                _ = RunStressAsync(window);
            else if (_layoutMode)
                _ = RunLayoutValidationAsync(window);

            WriteLog("START", $"{AppVersionInfo.ProductTitle} main window shown successfully.");
        }
        catch (Exception ex)
        {
            HandleFatalStartupException(ex);
        }
    }

    private async Task RunStressAsync(MainWindow window)
    {
        var report = string.IsNullOrWhiteSpace(_stressReportPath)
            ? Path.Combine(LogDirectory, $"Partcounter_Stress_{DateTime.Now:yyyyMMdd_HHmmss}.txt")
            : Environment.ExpandEnvironmentVariables(_stressReportPath);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var exitCode = await new ApplicationStressService().RunAsync(window, report, timeout.Token);
        WriteLog("STRESS", $"Stresslauf beendet. ExitCode={exitCode}; Report={report}");
        Shutdown(exitCode);
    }

    private async Task RunLayoutValidationAsync(MainWindow window)
    {
        var report = string.IsNullOrWhiteSpace(_layoutReportPath)
            ? Path.Combine(LogDirectory, $"Partcounter_Layout_{DateTime.Now:yyyyMMdd_HHmmss}.txt")
            : Environment.ExpandEnvironmentVariables(_layoutReportPath);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var exitCode = await new LayoutValidationService().RunAsync(window, report, timeout.Token);
        WriteLog("LAYOUT", $"Layout-Smoke-Test beendet. ExitCode={exitCode}; Report={report}");
        Shutdown(exitCode);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog("DISPATCHER_FATAL", e.Exception.ToString());
        e.Handled = true;

        if (_stressMode || _layoutMode)
        {
            Shutdown(91);
            return;
        }

        if (_dispatcherErrorDialogShown)
            return;

        _dispatcherErrorDialogShown = true;
        MessageBox.Show(
            $"{AppVersionInfo.ProductTitle} hat einen unerwarteten Fehler festgestellt.\n\n{e.Exception.Message}\n\nDiagnose:\n{LogPath}",
            $"{AppVersionInfo.ProductTitle} – Fehler",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        WriteLog("APPDOMAIN_FATAL", e.ExceptionObject?.ToString() ?? "Unknown AppDomain exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteLog("TASK_UNOBSERVED", e.Exception.ToString());
        e.SetObserved();
    }

    private void HandleFatalStartupException(Exception ex)
    {
        WriteLog("STARTUP_FATAL", ex.ToString());

        if (_stressMode || _layoutMode)
        {
            Shutdown(92);
            return;
        }

        try
        {
            MessageBox.Show(
                $"{AppVersionInfo.ProductTitle} konnte nicht gestartet werden.\n\n{ex.Message}\n\nEine Diagnose wurde gespeichert unter:\n{LogPath}",
                $"{AppVersionInfo.ProductTitle} – Startfehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Shutdown(-1);
        }
    }

    private static void WriteLog(string category, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }
        catch
        {
            // Logging must never prevent application startup.
        }
    }
}
