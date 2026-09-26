using System.Collections.Concurrent;

namespace MuscleCuties.App.Services.Navigation;

public sealed class NavigationContextService : INavigationContextService
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
    }

    public bool TryTake<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        value = default;

        if (!_values.TryRemove(key, out var stored) || stored is null)
            return false;

        if (stored is T typed)
        {
            value = typed;
            return true;
        }

        if (typeof(T).IsEnum && stored is string text &&
            Enum.TryParse(typeof(T), text, true, out var parsed))
        {
            value = (T)parsed;
            return true;
        }

        try
        {
            value = (T)Convert.ChangeType(stored, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
