using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.UI.Services;

public sealed class SecureStorageService : ITokenStorage
{
    private const string LegacyFallbackPrefix = "secure_storage_fallback_";
    private const string LegacyFallbackKeysKey = "secure_storage_fallback_keys";
    private static readonly Lazy<Task> LegacyCleanup = new(() => Task.Run(PurgeLegacyFallbackKeys));
    public void Remove(string key)
    {
        SecureStorage.Default.Remove(key);

        Preferences.Default.Remove(LegacyFallbackPrefix + key);
    }

    public void RemoveAll()
    {
        SecureStorage.Default.RemoveAll();

        PurgeLegacyFallbackKeys();
    }

    public async Task<string?> GetAsync(string key)
    {
        await LegacyCleanup.Value;
        return await SecureStorage.Default.GetAsync(key);
    }

    public async Task SetAsync(string key, string value)
    {
        await LegacyCleanup.Value;
        await SecureStorage.Default.SetAsync(key, value);
    }

    private static void PurgeLegacyFallbackKeys()
    {
        var keys = Preferences.Default.Get(LegacyFallbackKeysKey, string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in keys)
            Preferences.Default.Remove(LegacyFallbackPrefix + key);

        Preferences.Default.Remove(LegacyFallbackKeysKey);
    }

}
