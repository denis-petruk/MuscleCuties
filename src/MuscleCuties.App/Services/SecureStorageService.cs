using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App.Services;

public class SecureStorageService : ITokenStorage
{
    private const string FallbackPrefix = "secure_storage_fallback_";
    private const string FallbackKeysKey = "secure_storage_fallback_keys";

    public void Remove(string key)
    {
#if DEBUG && IOS
        if (UseDebugFallback())
        {
            Preferences.Default.Remove(BuildFallbackKey(key));
            UntrackFallbackKey(key);
            return;
        }
#endif

        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch when (UseDebugFallback())
        {
        }

        Preferences.Default.Remove(BuildFallbackKey(key));
        UntrackFallbackKey(key);
    }

    public void RemoveAll()
    {
#if DEBUG && IOS
        if (UseDebugFallback())
        {
            foreach (var key in GetFallbackKeys())
                Preferences.Default.Remove(BuildFallbackKey(key));

            Preferences.Default.Remove(FallbackKeysKey);
            return;
        }
#endif

        try
        {
            SecureStorage.Default.RemoveAll();
        }
        catch when (UseDebugFallback())
        {
        }

        foreach (var key in GetFallbackKeys())
            Preferences.Default.Remove(BuildFallbackKey(key));

        Preferences.Default.Remove(FallbackKeysKey);
    }

    public async Task<string?> GetAsync(string key)
    {
#if DEBUG && IOS
        if (UseDebugFallback())
        {
            var fallbackValue = Preferences.Default.Get(BuildFallbackKey(key), string.Empty);
            return string.IsNullOrWhiteSpace(fallbackValue) ? null : fallbackValue;
        }
#endif

        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch when (UseDebugFallback())
        {
            var fallbackValue = Preferences.Default.Get(BuildFallbackKey(key), string.Empty);
            return string.IsNullOrWhiteSpace(fallbackValue) ? null : fallbackValue;
        }
    }

    public async Task SetAsync(string key, string value)
    {
#if DEBUG && IOS
        if (UseDebugFallback())
        {
            Preferences.Default.Set(BuildFallbackKey(key), value);
            TrackFallbackKey(key);
            return;
        }
#endif

        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch when (UseDebugFallback())
        {
            Preferences.Default.Set(BuildFallbackKey(key), value);
            TrackFallbackKey(key);
        }
    }

    private static string BuildFallbackKey(string key)
    {
        return $"{FallbackPrefix}{key}";
    }

    private static void TrackFallbackKey(string key)
    {
        var keys = GetFallbackKeys().ToHashSet(StringComparer.Ordinal);
        if (!keys.Add(key))
            return;

        Preferences.Default.Set(FallbackKeysKey, string.Join("|", keys));
    }

    private static void UntrackFallbackKey(string key)
    {
        var keys = GetFallbackKeys().Where(storedKey => storedKey != key).ToArray();
        if (keys.Length == 0)
        {
            Preferences.Default.Remove(FallbackKeysKey);
            return;
        }

        Preferences.Default.Set(FallbackKeysKey, string.Join("|", keys));
    }

    private static IReadOnlyList<string> GetFallbackKeys()
    {
        return Preferences.Default
            .Get(FallbackKeysKey, string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool UseDebugFallback()
    {
#if DEBUG && IOS
        return DeviceInfo.Current.DeviceType == DeviceType.Virtual;
#else
        return false;
#endif
    }
}
