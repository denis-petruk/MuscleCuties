
namespace MuscleCuties.UI.Services.Localization;

/// <summary>The app's copy. English strings are registered by feature partials, Russian lives in</summary>
/// <see cref="RussianUiText" />, and text that arrives from the database is translated by exact match.</summary>
public static partial class UiTextCatalog
{
    private const char HashKeyPrefix = 'T';
    private const int HashKeyLength = 11;

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Tab_Today"] = "Today",
        ["Tab_Cycle"] = "Cycle",
        ["Tab_Train"] = "Train",
        ["Tab_Nutrition"] = "Nutrition",
        ["Tab_You"] = "You",
        ["Nutrition_IngredientsGramsHeader"] = "Ingredients · grams",
        ["Nutrition_MealTypeHeader"] = "Meal type",
        ["Progress_SelfWorthNote"] = "Use it to test progress, not to prove your worth.",
        ["NutritionSettings_SaveFailed"] = "Nutrition settings could not be saved. Please try again.",
        ["Dashboard_HydrationValueFormat"] = "Hydration: {0}",
    };

    /// <summary>Loads the catalog and registers every string group.</summary>
    static UiTextCatalog()
    {
        RegisterDashboardStrings();
        RegisterCycleWorkoutStrings();
        RegisterNutritionProfileAuthStrings();
        RegisterWorkoutGenerationStrings();
        RegisterWorkoutPhaseAdjustmentStrings();
        RegisterHydrationStrings();
        RegisterBodyMeasurementStrings();
        // Server-seeded quiz, exercise and session copy is translated by exact English text.
        foreach (var (english, russian) in SeededCopyRussian)
            RuntimeRussian.TryAdd(english, russian);
        AuthoredKeyByEnglish = English
            .Where(pair => !IsHashKey(pair.Key))
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
    }

    /// <summary>Declares the dashboard string registration.</summary>
    static partial void RegisterDashboardStrings();
    /// <summary>Declares the cycle and workout string registration.</summary>
    static partial void RegisterCycleWorkoutStrings();
    /// <summary>Declares the nutrition, profile and auth string registration.</summary>
    static partial void RegisterNutritionProfileAuthStrings();
    /// <summary>Declares the workout generation string registration.</summary>
    static partial void RegisterWorkoutGenerationStrings();
    /// <summary>Declares the phase adjustment string registration.</summary>
    static partial void RegisterWorkoutPhaseAdjustmentStrings();
    /// <summary>Declares the hydration string registration.</summary>
    static partial void RegisterHydrationStrings();
    /// <summary>Declares the body measurement string registration.</summary>
    static partial void RegisterBodyMeasurementStrings();

    private static readonly Dictionary<string, string> AuthoredKeyByEnglish;

    /// <summary>True when the key is registered, used before resolving text that may be a plain string.</summary>
    internal static bool HasKey(string key)
    {
        return English.ContainsKey(key);
    }

    /// <summary>The text for a key in the active language, falling back to English and then to the key itself,</summary>
    /// so a missing entry is visible instead of blank.</summary>
    public static string Get(string key)
    {
        if (!English.TryGetValue(key, out var english))
            return key;
        if (LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode &&
            RussianUiText.Values.TryGetValue(key, out var russian))
        {
            return russian;
        }
        return english;
    }

    /// <summary>Resolves a format string and fills it with composite formatting in the current culture.</summary>
    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    /// <summary>Recognises generated keys (<c>T</c> plus ten hex digits), which are excluded when English text is</summary>
    /// mapped back to an authored key.</summary>
    private static bool IsHashKey(string key)
    {
        if (key.Length != HashKeyLength || key[0] != HashKeyPrefix)
            return false;
        for (var index = 1; index < key.Length; index++)
            if (!char.IsAsciiHexDigit(key[index]))
                return false;
        return true;
    }

}
