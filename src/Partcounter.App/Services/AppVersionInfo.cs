using System.Reflection;

namespace Partcounter.Services;

/// <summary>
/// Zentrale Versionsquelle der laufenden Partcounter-Anwendung.
/// Die sichtbare Revision wird ausschließlich aus der Assembly-Version abgeleitet,
/// damit UI, Support und Diagnose nicht mehr auf veraltete hart codierte Revisionsstände zurückfallen.
/// </summary>
public static class AppVersionInfo
{
    private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
    private static readonly Version ExecutingVersion =
        ExecutingAssembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static Version Version => ExecutingVersion;

    public static string VersionText =>
        $"{Math.Max(0, ExecutingVersion.Major)}.{Math.Max(0, ExecutingVersion.Minor)}.{Math.Max(0, ExecutingVersion.Build)}";

    /// <summary>
    /// Partcounter-Revisionsschema: Assembly 0.1.21 => R001.21.
    /// </summary>
    public static string Revision =>
        $"R{Math.Max(0, ExecutingVersion.Minor):000}.{Math.Max(0, ExecutingVersion.Build):00}";

    public static string InformationalVersion =>
        ExecutingAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? VersionText;

    public static string ProductTitle => $"Partcounter {Revision}";

    public static string SimulationStatus => $"{Revision} · SIMULATION";

    public static string ProductionStatus => $"{Revision} · ECHTBETRIEB MODBUS TCP";

    public static string InstalledText => $"Installiert: {Revision} / {VersionText}";
}
