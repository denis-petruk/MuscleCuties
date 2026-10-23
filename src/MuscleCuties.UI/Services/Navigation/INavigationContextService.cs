using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.UI.Services.Navigation;

public interface INavigationContextService
{
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Standard key-value dictionary Set semantic.")]
    void Set<T>(string key, T value);

    bool TryTake<T>(string key, out T? value);
}
