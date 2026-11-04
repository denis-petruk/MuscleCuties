namespace MuscleCuties.UI.Services.Localization;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    void SetLanguage(string languageCode);
}
