namespace MuscleCuties.App.Services.Navigation;

public interface INavigationContextService
{
    void Set<T>(string key, T value);
    bool TryTake<T>(string key, out T? value);
}
