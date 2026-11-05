namespace MuscleCuties.UI.Services.Localization;

// The linked view model only needs localization when reporting a failure.
internal static class UiTextCatalog
{
    public static string Get(string key) => key;
    public static string Format(string key, params object[] args) => $"{key}:{string.Join(',', args)}";
}
