namespace MuscleCuties.UI.Services.Localization;

/// <summary>Reads and switches the app language. Switching also switches the formatting culture, so dates</summary>
/// and numbers follow the language rather than the device region.</summary>
public interface ILocalizationService
{
    string CurrentLanguage { get; }
    void SetLanguage(string languageCode);
}
