using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Partcounter.Services;

/// <summary>
/// Erzeugt ein Supportpaket mit der tatsächlich laufenden Partcounter-Version.
/// Der bestehende Diagnosekern wird weiterverwendet; die sichtbare Paket- und Manifestversion
/// wird anschließend auf die zentrale AppVersionInfo normalisiert.
/// </summary>
public sealed class SupportDiagnosticService
{
    private readonly ProductionReadinessService _readiness;

    public SupportDiagnosticService(ProductionReadinessService readiness) =>
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));

    public async Task<string> CreateCurrentVersionPackageAsync()
    {
        var generatedPath = await _readiness.CreateDiagnosticPackageAsync();
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var revisionFilePart = AppVersionInfo.Revision.Replace('.', '_');
        var targetPath = Path.Combine(
            _readiness.DiagnosticDirectory,
            $"Partcounter_Diagnose_{revisionFilePart}_{stamp}.zip");

        if (string.Equals(generatedPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            RewriteManifest(targetPath);
            return targetPath;
        }

        if (File.Exists(targetPath))
        {
            targetPath = Path.Combine(
                _readiness.DiagnosticDirectory,
                $"Partcounter_Diagnose_{revisionFilePart}_{stamp}_{Guid.NewGuid():N}.zip");
        }

        File.Move(generatedPath, targetPath);
        RewriteManifest(targetPath);
        return targetPath;
    }

    private void RewriteManifest(string packagePath)
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
        archive.GetEntry("manifest.txt")?.Delete();

        var entry = archive.CreateEntry("manifest.txt", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(BuildManifest());
    }

    private string BuildManifest()
    {
        var latestBackup = _readiness.GetLatestBackup();
        var sb = new StringBuilder();
        sb.AppendLine("PARTCOUNTER SUPPORT- / DIAGNOSEPAKET");
        sb.AppendLine($"Revision: {AppVersionInfo.Revision}");
        sb.AppendLine($"Version: {AppVersionInfo.VersionText}");
        sb.AppendLine($"Build: {AppVersionInfo.InformationalVersion}");
        sb.AppendLine($"Erstellt lokal: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Erstellt UTC: {DateTime.UtcNow:O}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Prozessarchitektur: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"App-Verzeichnis: {AppContext.BaseDirectory}");
        sb.AppendLine($"Datenverzeichnis: {_readiness.DataDirectory}");
        sb.AppendLine($"Datenbank: {_readiness.DatabasePath}");
        sb.AppendLine($"Letzte Sicherung: {(latestBackup is null ? "keine" : latestBackup.FullName)}");
        sb.AppendLine();
        sb.AppendLine("Datenschutz / Sicherheit:");
        sb.AppendLine("Das Supportpaket enthält keine Settings-Tabelle und keine Datenbanksicherung.");
        sb.AppendLine("Enthalten sind Systeminformationen, SQLite-Prüfergebnis, Startprotokoll und die letzten Ereigniseinträge.");
        sb.AppendLine("Passwörter, Tokens und API-Keys werden von dieser Funktion nicht aus den Einstellungen exportiert.");
        return sb.ToString();
    }
}
