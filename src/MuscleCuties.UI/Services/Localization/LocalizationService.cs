namespace MuscleCuties.UI.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, string> RuStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab_Today"] = "Сегодня",
        ["Tab_Cycle"] = "Цикл",
        ["Tab_Train"] = "Тренировки",
        ["Tab_Nutrition"] = "Питание",
        ["Tab_You"] = "Профиль"
    };

    private static readonly Dictionary<string, string> EnStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab_Today"] = "Today",
        ["Tab_Cycle"] = "Cycle",
        ["Tab_Train"] = "Train",
        ["Tab_Nutrition"] = "Nutrition",
        ["Tab_You"] = "You"
    };

    public LocalizationService()
    {
        CurrentLanguage = ActiveLanguage;
    }

    internal static string ActiveLanguage { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";

    public string CurrentLanguage { get; private set; }

    public void SetLanguage(string languageCode)
    {
        if (string.Equals(languageCode, "ru", StringComparison.OrdinalIgnoreCase))
            CurrentLanguage = "ru";
        else
            CurrentLanguage = "en";
        ActiveLanguage = CurrentLanguage;
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var dict = CurrentLanguage == "ru" ? RuStrings : EnStrings;
        if (dict.TryGetValue(key, out var val))
            return val;

        // Fallback to English
        if (EnStrings.TryGetValue(key, out var enVal))
            return enVal;

        return key;
    }

    public string this[string key] => GetString(key);
}
