using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App.Services;

public sealed class SecureStorageService : ITokenStorage
{
    private const string LegacyFallbackPrefix = "secure_storage_fallback_";
    private const string LegacyFallbackKeysKey = "secure_storage_fallback_keys";
    private static readonly Lazy<Task> LegacyCleanup = new(() => Task.Run(PurgeLegacyFallbackKeys));
    private static readonly Dictionary<string, string> SimulatorSession = new(StringComparer.Ordinal);
    private static readonly object SimulatorSessionGate = new();

    public void Remove(string key)
    {
        if (UseSimulatorSession())
        {
            lock (SimulatorSessionGate)
                SimulatorSession.Remove(key);
        }
        else
        {
            SecureStorage.Default.Remove(key);
        }

        Preferences.Default.Remove(LegacyFallbackPrefix + key);
    }

    public void RemoveAll()
    {
        if (UseSimulatorSession())
        {
            lock (SimulatorSessionGate)
                SimulatorSession.Clear();
        }
        else
        {
            SecureStorage.Default.RemoveAll();
        }

        PurgeLegacyFallbackKeys();
    }

    public async Task<string?> GetAsync(string key)
    {
        await LegacyCleanup.Value;
        if (UseSimulatorSession())
        {
            lock (SimulatorSessionGate)
                return SimulatorSession.GetValueOrDefault(key);
        }

        return await SecureStorage.Default.GetAsync(key);
    }

    public async Task SetAsync(string key, string value)
    {
        await LegacyCleanup.Value;
        if (UseSimulatorSession())
        {
            lock (SimulatorSessionGate)
                SimulatorSession[key] = value;
            return;
        }

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

    private static bool UseSimulatorSession()
    {
#if DEBUG && IOS
        return DeviceInfo.Current.DeviceType == DeviceType.Virtual;
#else
        return false;
#endif
    }
}
