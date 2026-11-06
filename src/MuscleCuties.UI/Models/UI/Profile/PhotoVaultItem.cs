using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Profile;

public partial class PhotoVaultItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RevealedImagePath))]
    [NotifyPropertyChangedFor(nameof(IsImageShown))]
    [NotifyPropertyChangedFor(nameof(IsImageHidden))]
    [NotifyPropertyChangedFor(nameof(AccessibilityDescription))]
    private bool _isRevealed;
    public int Id { get; init; }
    public string ImagePath { get; init; } = string.Empty;
    public bool IsImageAvailable { get; init; }
    public string? RevealedImagePath => IsRevealed && IsImageAvailable ? ImagePath : null;
    public bool IsImageShown => IsRevealed && IsImageAvailable;
    public bool IsImageHidden => !IsRevealed || !IsImageAvailable;
    public DateTime Date { get; init; }
    public double? WeightKg { get; init; }
    public string Notes { get; init; } = string.Empty;
    public CyclePhase Phase { get; init; }

    // Month names and number separators follow the app's selected language, not the device region.
    private static CultureInfo DisplayCulture => CultureInfo.GetCultureInfo(
        LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode ? "ru-RU" : "en-US");

    public string DateFormatted => Date.ToString(UiTextCatalog.Get("Profile_PhotoJourney_DateFormat"), DisplayCulture);

    public string WeightFormatted => WeightKg.HasValue
        ? string.Format(DisplayCulture, "{0:0.#} {1}", WeightKg.Value, UiTextCatalog.Get("Common_Unit_Kg"))
        : string.Empty;

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

    public bool HasWeight => WeightKg.HasValue;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public string PhaseName => Phase.ToString();
}
