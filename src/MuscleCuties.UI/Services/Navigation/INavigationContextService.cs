using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.UI.Services.Navigation;

/// <summary>Hands a value from one screen to the next without putting it in a route string or</summary>
/// keeping it in the destination's state. Reading removes the value, so it is delivered once.</summary>
public interface INavigationContextService
{
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Standard key-value dictionary Set semantic.")]
    /// <summary>Stores a value for the next screen; a later call with the same key replaces it.</summary>
    void Set<T>(string key, T value);

    /// <summary>Takes the stored value and removes it. Returns false when nothing was stored for the key.</summary>
    bool TryTake<T>(string key, out T? value);
}
