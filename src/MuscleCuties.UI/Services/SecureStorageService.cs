using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.UI.Services;

/// <summary>Token storage on top of the platform keychain. It also purges the keys an older build</summary>
/// kept in plain preferences, so no credential survives the upgrade.</summary>
public sealed class SecureStorageService : ITokenStorage
{
    private const string LegacyFallbackPrefix = "secure_storage_fallback_";
    private const string LegacyFallbackKeysKey = "secure_storage_fallback_keys";
    private static readonly Lazy<Task> LegacyCleanup = new(() => Task.Run(PurgeLegacyFallbackKeys));
    /// <summary>Removes a stored value, along with the plain-preferences copy an older build may have left.</summary>
    public void Remove(string key)
    {
        SecureStorage.Default.Remove(key);

        Preferences.Default.Remove(LegacyFallbackPrefix + key);
    }

    /// <summary>Clears everything the keychain holds for the app and any legacy preference keys.</summary>
    public void RemoveAll()
    {
        SecureStorage.Default.RemoveAll();

        PurgeLegacyFallbackKeys();
    }

    /// <summary>Reads a value once the legacy purge has finished, so a stale copy is never returned.</summary>
    public async Task<string?> GetAsync(string key)
    {
        await LegacyCleanup.Value;
        return await SecureStorage.Default.GetAsync(key);
    }

    /// <summary>Writes a value after the legacy purge, so the write cannot be undone by the cleanup.</summary>
    public async Task SetAsync(string key, string value)
    {
        await LegacyCleanup.Value;
        await SecureStorage.Default.SetAsync(key, value);
    }

    /// <summary>Deletes every key an older build wrote to plain preferences and forgets the key list.</summary>
    private static void PurgeLegacyFallbackKeys()
    {
        var keys = Preferences.Default.Get(LegacyFallbackKeysKey, string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in keys)
            Preferences.Default.Remove(LegacyFallbackPrefix + key);

        Preferences.Default.Remove(LegacyFallbackKeysKey);
    }

}
