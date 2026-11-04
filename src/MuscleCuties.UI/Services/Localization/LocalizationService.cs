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
    }
}
