
namespace MuscleCuties.UI.Services.Localization;

/// <summary>Holds the active language for the whole app and reports it to every caller that formats or</summary>
/// translates.</summary>
public sealed class LocalizationService : ILocalizationService
{
    internal const string RussianLanguageCode = "ru";
    internal const string EnglishLanguageCode = "en";

    /// <summary>Starts on the language the process was launched with, so the first frame is already localized.</summary>
    public LocalizationService()
    {
        CurrentLanguage = ActiveLanguage;
    }

    /// <summary>The language every static catalog and formatter reads, so they do not need a service reference.</summary>
    internal static string ActiveLanguage { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(RussianLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? RussianLanguageCode
            : EnglishLanguageCode;

    /// <summary>The language this instance last saw, for callers that need to react to a change.</summary>
    public string CurrentLanguage { get; private set; }

    /// <summary>Switches to Russian or English, falls back to English for anything else, and re-points the</summary>
    /// default culture so time and date formats match the copy.</summary>
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
