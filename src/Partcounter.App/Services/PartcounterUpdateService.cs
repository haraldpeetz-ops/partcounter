using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Partcounter.Services;

public sealed class PartcounterUpdateManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Product { get; set; } = "Partcounter";
    public string Version { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string Architecture { get; set; } = "win-x64";
    public string PayloadRoot { get; set; } = "payload/";
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool RequireAuthenticode { get; set; }
    public string PublisherCertificateThumbprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed record PartcounterUpdatePackage(
    string PackagePath,
    PartcounterUpdateManifest Manifest,
    Version TargetVersion,
    Version CurrentVersion,
    bool IsNewer,
    int PayloadFileCount);

public sealed class PartcounterUpdateService
{
    private const string ManifestName = "partcounter-update.json";
    private const string ChecksumName = "payload-sha256.txt";
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Version CurrentVersion => AppVersionInfo.Version;
    public string CurrentRevision => AppVersionInfo.Revision;
    public string UpdateRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Partcounter", "Updates");

    public async Task<PartcounterUpdatePackage> InspectPackageAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Updatepaket fehlt.", nameof(packagePath));
        packagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Updatepaket wurde nicht gefunden.", packagePath);
        if (!string.Equals(Path.GetExtension(packagePath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Partcounter-Updates müssen als ZIP-Paket vorliegen.");

        await using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var manifestEntry = archive.GetEntry(ManifestName)
            ?? throw new InvalidDataException($"{ManifestName} fehlt im Updatepaket.");

        PartcounterUpdateManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        using (var reader = new StreamReader(manifestStream, Encoding.UTF8, true, leaveOpen: false))
        {
            var json = await reader.ReadToEndAsync(cancellationToken);
            manifest = JsonSerializer.Deserialize<PartcounterUpdateManifest>(json, _jsonOptions);
        }

        if (manifest is null)
            throw new InvalidDataException("Update-Manifest ist ungültig.");
        if (manifest.SchemaVersion != 1)
            throw new InvalidDataException($"Nicht unterstützte Update-SchemaVersion {manifest.SchemaVersion}.");
        if (!string.Equals(manifest.Product, "Partcounter", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Das Paket gehört nicht zum Produkt Partcounter.");
        if (!Version.TryParse(manifest.Version, out var targetVersion))
            throw new InvalidDataException($"Ungültige Zielversion '{manifest.Version}'.");
        if (!string.Equals(manifest.Architecture, "win-x64", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Updatearchitektur '{manifest.Architecture}' wird von dieser Ausgabe nicht unterstützt.");

        var root = NormalizePayloadRoot(manifest.PayloadRoot);
        var payloadEntries = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && e.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (payloadEntries.Count == 0)
            throw new InvalidDataException("Das Updatepaket enthält keinen Payload.");
        if (!payloadEntries.Any(e => string.Equals(GetRelativePayloadName(e.FullName, root), "Partcounter.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Der Payload enthält keine Partcounter.exe.");

        await VerifyChecksumsAsync(archive, payloadEntries, root, cancellationToken);

        return new PartcounterUpdatePackage(
            packagePath,
            manifest,
            targetVersion,
            CurrentVersion,
            targetVersion > CurrentVersion,
            payloadEntries.Count);
    }

    public async Task<PartcounterUpdatePackage?> FindLatestPackageAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Updateverzeichnis fehlt.", nameof(directory));
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Updateverzeichnis nicht gefunden: {directory}");

        PartcounterUpdatePackage? best = null;
        foreach (var file in Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var package = await InspectPackageAsync(file, cancellationToken);
                if (!package.IsNewer)
                    continue;
                if (best is null || package.TargetVersion > best.TargetVersion)
                    best = package;
            }
            catch
            {
                // Fremde/defekte ZIP-Dateien in einem Netzwerkordner werden ignoriert.
            }
        }
        return best;
    }

    public async Task<string> StageAndScheduleInstallAsync(PartcounterUpdatePackage package, CancellationToken cancellationToken = default)
    {
        if (!package.IsNewer)
            throw new InvalidOperationException("Das ausgewählte Paket ist nicht neuer als die installierte Version.");

        EnsureInstallDirectoryWritable();
        Directory.CreateDirectory(UpdateRoot);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var staging = Path.Combine(UpdateRoot, "Staging", $"{package.Manifest.Revision}_{stamp}");
        var backup = Path.Combine(UpdateRoot, "Backups", $"{CurrentRevision}_vor_{package.Manifest.Revision}_{stamp}");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(backup);

        await ExtractPayloadAsync(package.PackagePath, package.Manifest.PayloadRoot, staging, cancellationToken);
        var stagedExe = Path.Combine(staging, "Partcounter.exe");
        if (!File.Exists(stagedExe))
            throw new InvalidDataException("Staging enthält keine Partcounter.exe.");
        if (package.Manifest.RequireAuthenticode)
            VerifyAuthenticode(stagedExe, package.Manifest.PublisherCertificateThumbprint);

        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der aktuelle Programmpfad konnte nicht ermittelt werden.");
        var installDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var exeName = Path.GetFileName(processPath);
        var logPath = Path.Combine(UpdateRoot, "update.log");
        var scriptPath = Path.Combine(UpdateRoot, $"apply_{package.Manifest.Revision}_{stamp}.ps1");

        var script = BuildInstallerScript(
            Environment.ProcessId,
            staging,
            installDirectory,
            backup,
            exeName,
            logPath,
            package.Manifest.Revision);
        await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = installDirectory
        };
        if (Process.Start(psi) is null)
            throw new InvalidOperationException("Der Update-Installationsprozess konnte nicht gestartet werden.");
        return backup;
    }

    private async Task VerifyChecksumsAsync(
        ZipArchive archive,
        IReadOnlyList<ZipArchiveEntry> payloadEntries,
        string root,
        CancellationToken cancellationToken)
    {
        var checksumEntry = archive.GetEntry(ChecksumName)
            ?? throw new InvalidDataException($"{ChecksumName} fehlt im Updatepaket.");
        string checksumText;
        await using (var checksumStream = checksumEntry.Open())
        using (var reader = new StreamReader(checksumStream, Encoding.UTF8, true, leaveOpen: false))
            checksumText = await reader.ReadToEndAsync(cancellationToken);

        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in checksumText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            if (separator <= 0) continue;
            var hash = line[..separator].Trim();
            var relative = line[(separator + 2)..].Trim().Replace('\\', '/');
            expected[relative] = hash;
        }

        foreach (var entry in payloadEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = GetRelativePayloadName(entry.FullName, root).Replace('\\', '/');
            if (!expected.TryGetValue(relative, out var expectedHash))
                throw new InvalidDataException($"Prüfsumme fehlt für '{relative}'.");
            await using var entryStream = entry.Open();
            using var sha = SHA256.Create();
            var actual = Convert.ToHexString(await sha.ComputeHashAsync(entryStream, cancellationToken)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(actual),
                    Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant())))
                throw new InvalidDataException($"SHA-256-Prüfung fehlgeschlagen: {relative}");
        }
    }

    private static async Task ExtractPayloadAsync(string packagePath, string payloadRoot, string staging, CancellationToken cancellationToken)
    {
        var root = NormalizePayloadRoot(payloadRoot);
        var stagingFull = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        await using var file = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Name) || !entry.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;
            var relative = GetRelativePayloadName(entry.FullName, root).Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(staging, relative));
            if (!destination.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unsicherer Pfad im Updatepaket erkannt.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    private static string NormalizePayloadRoot(string? value)
    {
        var root = string.IsNullOrWhiteSpace(value) ? "payload" : value.Trim().Trim('/', '\\');
        if (root.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Ungültiger PayloadRoot.");
        return root + "/";
    }

    private static string GetRelativePayloadName(string fullName, string root) =>
        fullName[root.Length..].TrimStart('/', '\\');

    private static void VerifyAuthenticode(string executablePath, string expectedThumbprint)
    {
        var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(executablePath);
        using var certificate2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
        if (string.IsNullOrWhiteSpace(certificate2.Thumbprint))
            throw new InvalidDataException("Das Update verlangt Authenticode, aber Partcounter.exe besitzt kein Signaturzertifikat.");
        if (!string.IsNullOrWhiteSpace(expectedThumbprint))
        {
            var expected = expectedThumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var actual = certificate2.Thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected)))
                throw new InvalidDataException("Das Signaturzertifikat des Updatepakets entspricht nicht dem freigegebenen Herausgeber.");
        }
    }

    private static void EnsureInstallDirectoryWritable()
    {
        var directory = Path.GetFullPath(AppContext.BaseDirectory);
        var probe = Path.Combine(directory, $".partcounter_update_probe_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(
                $"Der aktuelle Partcounter-Ordner ist nicht schreibbar. Ein In-Place-Update ist hier nicht möglich: {directory}", ex);
        }
    }

    private static string BuildInstallerScript(
        int processId,
        string staging,
        string install,
        string backup,
        string exeName,
        string logPath,
        string revision)
    {
        static string Q(string value) => "'" + value.Replace("'", "''") + "'";

        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine($"$pidToWait = {processId}");
        sb.AppendLine($"$staging = {Q(staging)}");
        sb.AppendLine($"$install = {Q(install)}");
        sb.AppendLine($"$backup = {Q(backup)}");
        sb.AppendLine($"$exeName = {Q(exeName)}");
        sb.AppendLine($"$log = {Q(logPath)}");
        sb.AppendLine($"$revision = {Q(revision)}");
        sb.AppendLine();
        sb.AppendLine("function Log([string]$text) {");
        sb.AppendLine("    $line = \"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') $text\"");
        sb.AppendLine("    Add-Content -LiteralPath $log -Value $line -Encoding UTF8");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("try {");
        sb.AppendLine("    Log \"Update $revision vorbereitet. Warte auf Prozess $pidToWait.\"");
        sb.AppendLine("    Wait-Process -Id $pidToWait -ErrorAction SilentlyContinue");
        sb.AppendLine("    Start-Sleep -Milliseconds 800");
        sb.AppendLine("    New-Item -ItemType Directory -Force -Path $backup | Out-Null");
        sb.AppendLine();
        sb.AppendLine("    Get-ChildItem -LiteralPath $staging -Recurse -File | ForEach-Object {");
        sb.AppendLine("        $relative = $_.FullName.Substring($staging.Length).TrimStart('\\')");
        sb.AppendLine("        $destination = Join-Path $install $relative");
        sb.AppendLine("        $destinationDir = Split-Path -Parent $destination");
        sb.AppendLine("        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null");
        sb.AppendLine("        if (Test-Path -LiteralPath $destination) {");
        sb.AppendLine("            $backupFile = Join-Path $backup $relative");
        sb.AppendLine("            $backupDir = Split-Path -Parent $backupFile");
        sb.AppendLine("            New-Item -ItemType Directory -Force -Path $backupDir | Out-Null");
        sb.AppendLine("            Copy-Item -LiteralPath $destination -Destination $backupFile -Force");
        sb.AppendLine("        }");
        sb.AppendLine("        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    Log \"Update $revision installiert. Backup: $backup\"");
        sb.AppendLine("    $exe = Join-Path $install $exeName");
        sb.AppendLine("    Start-Process -FilePath $exe -WorkingDirectory $install");
        sb.AppendLine("}");
        sb.AppendLine("catch {");
        sb.AppendLine("    Log \"UPDATEFEHLER: $($_.Exception.ToString())\"");
        sb.AppendLine("    try {");
        sb.AppendLine("        if (Test-Path -LiteralPath $backup) {");
        sb.AppendLine("            Get-ChildItem -LiteralPath $backup -Recurse -File | ForEach-Object {");
        sb.AppendLine("                $relative = $_.FullName.Substring($backup.Length).TrimStart('\\')");
        sb.AppendLine("                $destination = Join-Path $install $relative");
        sb.AppendLine("                $destinationDir = Split-Path -Parent $destination");
        sb.AppendLine("                New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null");
        sb.AppendLine("                Copy-Item -LiteralPath $_.FullName -Destination $destination -Force");
        sb.AppendLine("            }");
        sb.AppendLine("            Log \"Backup nach Updatefehler bestmöglich zurückgespielt.\"");
        sb.AppendLine("        }");
        sb.AppendLine("    } catch { Log \"ROLLBACKFEHLER: $($_.Exception.ToString())\" }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
