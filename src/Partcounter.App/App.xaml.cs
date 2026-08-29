using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Partcounter.Services;

namespace Partcounter;

public partial class App : Application
{
    private bool _dispatcherErrorDialogShown;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Partcounter");

    private static string LogPath => Path.Combine(LogDirectory, "Partcounter_startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        VersionUiService.Initialize();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            WriteLog(
                "START",
                $"{AppVersionInfo.ProductTitle} startup. Version={AppVersionInfo.VersionText}; Build={AppVersionInfo.InformationalVersion}; OS={Environment.OSVersion}; Runtime={Environment.Version}; Base={AppContext.BaseDirectory}");

            var window = new MainWindow();
            CompanyBrandingBootstrap.Attach(window);
            InfoUpdateHelpBootstrap.Attach(window);
            ProductionReadinessBootstrap.Attach(window);
            LiveCommissioningBootstrap.Attach(window);
            LabelReprintBootstrap.Attach(window);
            ProfessionalHelpBootstrap.Attach(window);
            SupportCenterBootstrap.Attach(window);
            VersionUiService.NormalizeWindow(window);
            MainWindow = window;
            window.Show();

            WriteLog("START", $"{AppVersionInfo.ProductTitle} main window shown successfully.");
        }
        catch (Exception ex)
        {
            HandleFatalStartupException(ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog("DISPATCHER_FATAL", e.Exception.ToString());
        e.Handled = true;

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
