using System.Reflection;

namespace Partcounter.Services;

/// <summary>
/// Zentrale Versionsquelle der laufenden Partcounter-Anwendung.
/// Hauptrevision und Hotfixstand werden aus Assembly-/FileVersion abgeleitet,
/// damit UI, Support und Diagnose immer denselben installierten Stand anzeigen.
/// </summary>
public static class AppVersionInfo
{
    private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
    private static readonly Version ExecutingVersion =
        ExecutingAssembly.GetName().Version ?? new Version(0, 0, 0, 0);
    private static readonly Version ExecutingFileVersion =
        Version.TryParse(
            ExecutingAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
            out var fileVersion)
            ? fileVersion
            : ExecutingVersion;

    public static Version Version => ExecutingVersion;
    public static Version FileVersion => ExecutingFileVersion;

    public static string VersionText =>
        $"{Math.Max(0, ExecutingVersion.Major)}.{Math.Max(0, ExecutingVersion.Minor)}.{Math.Max(0, ExecutingVersion.Build)}";

    /// <summary>
    /// Partcounter-Revisionsschema: Assembly 0.1.25 => R001.25.
    /// </summary>
    public static string Revision =>
        $"R{Math.Max(0, ExecutingVersion.Minor):000}.{Math.Max(0, ExecutingVersion.Build):00}";

    public static int Hotfix => Math.Max(0, ExecutingFileVersion.Revision);

    public static string RevisionLabel => Hotfix > 0 ? $"{Revision} HF{Hotfix}" : Revision;

    public static string InformationalVersion =>
        ExecutingAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? VersionText;

    public static string ProductTitle => $"Partcounter {RevisionLabel}";

    public static string SimulationStatus => $"{RevisionLabel} · SIMULATION";

    public static string ProductionStatus => $"{RevisionLabel} · ECHTBETRIEB MODBUS TCP";

    public static string InstalledText =>
        $"Installiert: {RevisionLabel} / {VersionText} / FileVersion {ExecutingFileVersion}";
}
