using System.Security.Cryptography;
using System.Text.Json;

namespace Partcounter.Services;

public sealed class AdminAccessService
{
    private const int CredentialVersion = 1;
    private const int Pbkdf2Iterations = 210_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    public const int MinimumPasswordLength = 8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _credentialPath;
    private AdminCredential? _credential;
    private string? _loadError;

    public AdminAccessService()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Partcounter");
        Directory.CreateDirectory(baseDirectory);
        _credentialPath = Path.Combine(baseDirectory, "admin_access.json");
    }

    public bool IsConfigured => _credential is not null;
    public bool IsUnlocked { get; private set; }
    public bool HasCredentialError => !string.IsNullOrWhiteSpace(_loadError);
    public string? CredentialError => _loadError;
    public string CredentialPath => _credentialPath;

    public event EventHandler? StateChanged;

    public void Initialize()
    {
        IsUnlocked = false;
        _credential = null;
        _loadError = null;

        if (!File.Exists(_credentialPath))
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var json = File.ReadAllText(_credentialPath);
            var credential = JsonSerializer.Deserialize<AdminCredential>(json, JsonOptions);
            if (credential is null || credential.Version != CredentialVersion ||
                credential.Iterations < 100_000 || string.IsNullOrWhiteSpace(credential.SaltBase64) ||
                string.IsNullOrWhiteSpace(credential.HashBase64))
            {
                throw new InvalidDataException("Ungültiges Admin-Zugriffsprofil.");
            }

            _ = Convert.FromBase64String(credential.SaltBase64);
            _ = Convert.FromBase64String(credential.HashBase64);
            _credential = credential;
        }
        catch (Exception ex)
        {
            // Ein beschädigtes Zugriffsprofil darf den Produktionsbetrieb nicht blockieren.
            // Es sperrt jedoch bewusst alle administrativen Bereiche, bis das Profil repariert wird.
            _loadError = ex.Message;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryUnlock(string password)
    {
        if (_credential is null || HasCredentialError || string.IsNullOrEmpty(password))
            return false;

        try
        {
            var salt = Convert.FromBase64String(_credential.SaltBase64);
            var expected = Convert.FromBase64String(_credential.HashBase64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                _credential.Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            var valid = CryptographicOperations.FixedTimeEquals(actual, expected);
            CryptographicOperations.ZeroMemory(actual);
            if (!valid)
                return false;

            IsUnlocked = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetPassword(string password)
    {
        if (HasCredentialError)
            throw new InvalidOperationException("Das vorhandene Admin-Zugriffsprofil ist beschädigt und kann nicht überschrieben werden.");

        if (IsConfigured && !IsUnlocked)
            throw new InvalidOperationException("Zum Ändern des Admin-Passworts muss die Administration entsperrt sein.");

        ValidatePassword(password);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        try
        {
            var credential = new AdminCredential
            {
                Version = CredentialVersion,
                Iterations = Pbkdf2Iterations,
                SaltBase64 = Convert.ToBase64String(salt),
                HashBase64 = Convert.ToBase64String(hash),
                UpdatedAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(credential, JsonOptions);
            var tempPath = _credentialPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _credentialPath, true);

            _credential = credential;
            IsUnlocked = true;
            _loadError = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public void Lock()
    {
        if (!IsUnlocked)
            return;

        IsUnlocked = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
            throw new ArgumentException($"Das Admin-Passwort muss mindestens {MinimumPasswordLength} Zeichen lang sein.", nameof(password));
    }

    private sealed class AdminCredential
    {
        public int Version { get; set; }
        public int Iterations { get; set; }
        public string SaltBase64 { get; set; } = string.Empty;
        public string HashBase64 { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}
