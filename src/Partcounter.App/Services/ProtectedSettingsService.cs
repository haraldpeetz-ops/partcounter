using System.Security.Cryptography;
using System.Text;

namespace Partcounter.Services;

public sealed class ProtectedSettingsService
{
    private readonly DatabaseService _database;

    public ProtectedSettingsService(DatabaseService database)
    {
        _database = database;
    }

    public async Task SetSecretAsync(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            await _database.SetSettingAsync(key, string.Empty);
            return;
        }

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        await _database.SetSettingAsync(key, Convert.ToBase64String(protectedBytes));
    }

    public async Task<string> GetSecretAsync(string key)
    {
        var stored = await _database.GetSettingAsync(key);
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
