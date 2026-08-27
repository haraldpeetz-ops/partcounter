namespace Partcounter.Services;

public sealed class CompanyBrandingService
{
    private const long MaxLogoBytes = 10L * 1024L * 1024L;
    private const string StoredFileSetting = "CompanyLogoStoredFile";
    private const string OriginalNameSetting = "CompanyLogoOriginalFileName";

    private static readonly string[] SupportedExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    ];

    private readonly DatabaseService _database = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public static CompanyBrandingService Shared { get; } = new();

    private CompanyBrandingService()
    {
        BrandingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Partcounter",
            "Branding");
    }

    public string BrandingDirectory { get; }
    public string? CurrentLogoPath { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public bool HasLogo => !string.IsNullOrWhiteSpace(CurrentLogoPath) && File.Exists(CurrentLogoPath);

    public event EventHandler? Changed;

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_initialized)
                return;

            await _database.InitializeAsync();
            Directory.CreateDirectory(BrandingDirectory);

            var storedFile = await _database.GetSettingAsync(StoredFileSetting) ?? string.Empty;
            OriginalFileName = await _database.GetSettingAsync(OriginalNameSetting) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(storedFile))
            {
                var safeName = Path.GetFileName(storedFile);
                var candidate = Path.Combine(BrandingDirectory, safeName);
                if (File.Exists(candidate) && IsSupportedExtension(Path.GetExtension(candidate)))
                    CurrentLogoPath = candidate;
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetLogoAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Es wurde keine Bilddatei ausgewählt.", nameof(sourcePath));

        await InitializeAsync();

        var fullSource = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSource))
            throw new FileNotFoundException("Die gewählte Bilddatei wurde nicht gefunden.", fullSource);

        var extension = Path.GetExtension(fullSource).ToLowerInvariant();
        if (!IsSupportedExtension(extension))
            throw new InvalidOperationException("Unterstützt werden PNG, JPG/JPEG, BMP, GIF und TIFF.");

        var fileInfo = new FileInfo(fullSource);
        if (fileInfo.Length <= 0)
            throw new InvalidOperationException("Die gewählte Bilddatei ist leer.");
        if (fileInfo.Length > MaxLogoBytes)
            throw new InvalidOperationException("Das Firmenlogo darf maximal 10 MB groß sein.");

        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(BrandingDirectory);

            var normalizedExtension = extension == ".jpeg" ? ".jpg" : extension;
            var storedName = "company-logo" + normalizedExtension;
            var destination = Path.Combine(BrandingDirectory, storedName);
            var temporary = Path.Combine(BrandingDirectory, $"company-logo-{Guid.NewGuid():N}.tmp");

            File.Copy(fullSource, temporary, overwrite: true);

            foreach (var existing in Directory.EnumerateFiles(BrandingDirectory, "company-logo.*"))
            {
                if (!string.Equals(existing, temporary, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(existing); }
                    catch { }
                }
            }

            File.Move(temporary, destination, overwrite: true);

            CurrentLogoPath = destination;
            OriginalFileName = Path.GetFileName(fullSource);

            await _database.SetSettingAsync(StoredFileSetting, storedName);
            await _database.SetSettingAsync(OriginalNameSetting, OriginalFileName);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveLogoAsync()
    {
        await InitializeAsync();
        await _gate.WaitAsync();
        try
        {
            foreach (var existing in Directory.EnumerateFiles(BrandingDirectory, "company-logo.*"))
            {
                try { File.Delete(existing); }
                catch { }
            }

            CurrentLogoPath = null;
            OriginalFileName = string.Empty;
            await _database.SetSettingAsync(StoredFileSetting, string.Empty);
            await _database.SetSettingAsync(OriginalNameSetting, string.Empty);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsSupportedExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) &&
        SupportedExtensions.Contains(extension.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
}
