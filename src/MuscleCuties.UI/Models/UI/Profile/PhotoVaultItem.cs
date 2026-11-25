using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Profile;

/// <summary>A progress photo in the vault grid: when it was taken, its optional weight and cycle</summary>
/// phase, and whether the person has chosen to reveal it.</summary>
public partial class PhotoVaultItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RevealedImagePath))]
    [NotifyPropertyChangedFor(nameof(IsImageShown))]
    [NotifyPropertyChangedFor(nameof(IsImageHidden))]
    [NotifyPropertyChangedFor(nameof(AccessibilityDescription))]
    private bool _isRevealed;
    [ObservableProperty] private bool _isSelectedFirst;
    [ObservableProperty] private bool _isSelectedSecond;
    /// <summary>Server identity of the photo, used when deleting it.</summary>
    public int Id { get; init; }
    /// <summary>Cached local file. It is only used when <see cref="IsImageAvailable" /> is true.</summary>
    public string ImagePath { get; init; } = string.Empty;
    /// <summary>False when the cached file is gone, so the tile shows a placeholder rather than a</summary>
    /// broken image.</summary>
    public bool IsImageAvailable { get; init; }
    /// <summary>The file to render, or null while the photo is hidden or missing from the device.</summary>
    public string? RevealedImagePath => IsRevealed && IsImageAvailable ? ImagePath : null;
    /// <summary>True when the photo can be shown.</summary>
    public bool IsImageShown => IsRevealed && IsImageAvailable;
    /// <summary>True when the photo is hidden.</summary>
    public bool IsImageHidden => !IsRevealed || !IsImageAvailable;
    /// <summary>Local date the photo was taken.</summary>
    public DateTime Date { get; init; }
    /// <summary>Weight recorded alongside the photo, when the person entered one.</summary>
    public double? WeightKg { get; init; }
    /// <summary>Optional free-text note shown under the photo.</summary>
    public string Notes { get; init; } = string.Empty;
    /// <summary>Cycle phase at the time of the photo, used to tint its badge.</summary>
    public CyclePhase Phase { get; init; }

    /// <summary>Month names and number separators follow the app's selected language, not the device region.</summary>
    private static CultureInfo DisplayCulture => CultureInfo.GetCultureInfo(
        LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode ? "ru-RU" : "en-US");

    /// <summary>Date in the app language, not the device region.</summary>
    public string DateFormatted => Date.ToString(UiTextCatalog.Get("Profile_PhotoJourney_DateFormat"), DisplayCulture);

    /// <summary>Delete announcement that names the photo's date, so VoiceOver users can tell rows apart.</summary>
    public string DeleteAccessibilityDescription =>
        UiTextCatalog.Format("Profile_PhotoVault_DeleteA11yFormat", DateFormatted);

    /// <summary>Weight with its unit, or empty when the photo has none.</summary>
    public string WeightFormatted => WeightKg.HasValue
        ? string.Format(DisplayCulture, "{0:0.#} {1}", WeightKg.Value, UiTextCatalog.Get("Common_Unit_Kg"))
        : string.Empty;

    /// <summary>Tile announcement: the date plus whether the photo is revealed, hidden for privacy,</summary>
    /// or missing from the device.</summary>
    public string AccessibilityDescription
    {
        get
        {
            var state = !IsImageAvailable
                ? UiTextCatalog.Get("Profile_PhotoJourney_PhotoUnavailable")
                : IsRevealed
                    ? UiTextCatalog.Get("Profile_PhotoJourney_PhotoRevealed")
                    : UiTextCatalog.Get("Profile_PhotoJourney_PhotoHiddenForPrivacy");
            return $"{DateFormatted}. {state}";
        }
    }

    /// <summary>True when the entry has a weight.</summary>
    public bool HasWeight => WeightKg.HasValue;
    /// <summary>True when the entry has notes.</summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    /// <summary>Phase code as text, for callers that log or compare rather than display.</summary>
    public string PhaseName => Phase.ToString();
}
