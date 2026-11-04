using System.Globalization;
using System.Text.RegularExpressions;

namespace MuscleCuties.UI.Services.Localization;

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

    static UiTextCatalog()
    {
        RegisterDashboardStrings();
        RegisterCycleWorkoutStrings();
        RegisterNutritionProfileAuthStrings();
        RegisterWorkoutGenerationStrings();
        RegisterWorkoutPhaseAdjustmentStrings();
        AuthoredKeyByEnglish = English
            .Where(pair => !IsHashKey(pair.Key))
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
    }

    static partial void RegisterDashboardStrings();
    static partial void RegisterCycleWorkoutStrings();
    static partial void RegisterNutritionProfileAuthStrings();
    static partial void RegisterWorkoutGenerationStrings();
    static partial void RegisterWorkoutPhaseAdjustmentStrings();

    private static readonly Dictionary<string, string> AuthoredKeyByEnglish;

    internal static IReadOnlyCollection<string> MissingRussianKeys =>
        English.Keys.Where(key => !RussianUiText.Values.ContainsKey(key)).ToArray();

    internal static IReadOnlyCollection<string> InvalidRussianFormatKeys => English
        .Where(pair => RussianUiText.Values.TryGetValue(pair.Key, out var russian) &&
                       !FormatArguments(pair.Value).SequenceEqual(FormatArguments(russian)))
        .Select(pair => pair.Key)
        .ToArray();

    internal static bool HasKey(string key)
    {
        return English.ContainsKey(key);
    }

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

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private static bool IsHashKey(string key)
    {
        if (key.Length != HashKeyLength || key[0] != HashKeyPrefix)
            return false;
        for (var index = 1; index < key.Length; index++)
            if (!char.IsAsciiHexDigit(key[index]))
                return false;
        return true;
    }

    private static string[] FormatArguments(string text)
    {
        return Regex.Matches(text, @"\{\d+(?::[^}]*)?\}")
            .Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
