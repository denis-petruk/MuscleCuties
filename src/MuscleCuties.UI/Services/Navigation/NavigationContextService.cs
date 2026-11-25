using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.UI.Services.Navigation;

/// <summary>Thread-safe one-shot handoff between screens; entries are removed as they are taken.</summary>
public sealed class NavigationContextService : INavigationContextService
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>Stores a value, overwriting any earlier one for the same key.</summary>
    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Enum types and value types used with TryTake are concretely referenced at call sites.")]
    /// <summary>Takes a value and removes it, converting string enums and primitives because a route</summary>
    /// query has already turned the original value into text.</summary>
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
            value = (T)Convert.ChangeType(stored, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T),
                CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
