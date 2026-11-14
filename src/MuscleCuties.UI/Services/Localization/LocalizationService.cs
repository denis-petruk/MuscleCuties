using System.Globalization;

namespace MuscleCuties.UI.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    internal const string RussianLanguageCode = "ru";
    internal const string EnglishLanguageCode = "en";

    public LocalizationService()
    {
        CurrentLanguage = ActiveLanguage;
    }

    internal static string ActiveLanguage { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(RussianLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? RussianLanguageCode
            : EnglishLanguageCode;

    public string CurrentLanguage { get; private set; }

    public void SetLanguage(string languageCode)
    {
        CurrentLanguage = string.Equals(languageCode, RussianLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? RussianLanguageCode
            : EnglishLanguageCode;
        ActiveLanguage = CurrentLanguage;

        // Date/number/time formatting must follow the selected language, not the OS locale.
        // Without this, RU copy rendered with a 12-hour clock from an en-US process culture.
        var culture = CultureInfo.GetCultureInfo(
            CurrentLanguage == RussianLanguageCode ? "ru-RU" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
