namespace MuscleCuties.UI.Services.Localization;

// The linked view model only needs localization when reporting a failure.
internal static class UiTextCatalog
{
    public static string Get(string key) => key switch
    {
        // Simulates missing localization copy for a catalog key that the app resolves.
        "Workout_Generation_EmptyCopy" => string.Empty,
        "Workout_Generation_WhitespaceCopy" => "   ",
        _ => key
    };

    public static bool HasKey(string key) => key.StartsWith("Workout_", StringComparison.Ordinal);

    public static string Format(string key, params object[] args) => $"{key}:{string.Join(',', args)}";
}
