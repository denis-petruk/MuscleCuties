namespace MuscleCuties.UI.Services.Localization;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string this[string key] { get; }
    void SetLanguage(string languageCode);
    string GetString(string key);
}
