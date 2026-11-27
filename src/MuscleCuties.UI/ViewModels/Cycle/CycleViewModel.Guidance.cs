// Phase guidance copy and its loading and error states.
// Second half of CycleViewModel; see its declaration for the full contract.

using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.ViewModels.Cycle;

/// <summary>The phase guidance half of the cycle page: the explorer's phase pills, the tips for the phase she is in and</summary>
/// the summary sheet a phase opens.</summary>
public partial class CycleViewModel
{
    /// <summary>The heading that frames the guidance as context about her phase rather than a diagnosis.</summary>
    private static string PhaseContextText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_Context");
    /// <summary>The heading for the check-in prompt.</summary>
    private static string PhaseCheckInText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_CheckIn");
    /// <summary>The heading for the training tip.</summary>
    private static string PhaseTrainingText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_Training");
    /// <summary>The heading for the nutrition tip.</summary>
    private static string PhaseNutritionText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_Nutrition");
    /// <summary>The heading for the recovery tip.</summary>
    private static string PhaseRecoveryText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_Recovery");
    /// <summary>The note that phase timing varies between people, so the guidance is not read as a schedule.</summary>
    private static string PhaseTimingText =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_TimingVaries");
    /// <summary>The explorer's one-line invitation to browse the phases.</summary>
    private static string PhaseExplorerTagline =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_ExplorerTagline");
    [ObservableProperty] private string _dailyCheerText = string.Empty;
    [ObservableProperty] private string _dailyNutritionTipText = string.Empty;
    [ObservableProperty] private string _dailyTrainingTipText = string.Empty;
    [ObservableProperty] private bool _isPhaseSummarySheetVisible;
    [ObservableProperty] private string _phaseEnergyBadgeText = string.Empty;
    [ObservableProperty] private ObservableCollection<PhaseExplorerPillItem> _phaseExplorerPills = new();
    [ObservableProperty] private string _phaseHormonalSnapshot = string.Empty;
    [ObservableProperty] private string _phaseVectorArtworkData = string.Empty;
    [ObservableProperty] private PhaseSummarySheetModel? _selectedPhaseSummary;

    /// <summary>Opens the summary sheet for the phase whose pill was tapped.</summary>
    public RelayCommand<PhaseExplorerPillItem> SelectPhaseExplorerPillCommand { get; private set; } = null!;
    /// <summary>Dismisses the summary sheet without leaving the page.</summary>
    public RelayCommand ClosePhaseSummarySheetCommand { get; private set; } = null!;
    /// <summary>Goes on from the sheet to the full page for the phase it was describing.</summary>
    public IAsyncRelayCommand OpenSelectedPhaseDetailsCommand { get; private set; } = null!;

    /// <summary>Wires the guidance commands and fills the section for the phase the page is on.</summary>
    private void InitializeGuidanceCommands()
    {
        SelectPhaseExplorerPillCommand = new RelayCommand<PhaseExplorerPillItem>(SelectPhaseExplorerPill);
        ClosePhaseSummarySheetCommand = new RelayCommand(ClosePhaseSummarySheet);
        OpenSelectedPhaseDetailsCommand = new AsyncRelayCommand(OpenSelectedPhaseDetailsAsync);

        RefreshGuidanceContent(CurrentPhase, UseDarkTheme);
    }

    /// <summary>Builds the sheet for a pill's phase and shows it, ignoring a tap that carried no pill.</summary>
    private void SelectPhaseExplorerPill(PhaseExplorerPillItem? item)
    {
        if (item is null) return;
        SelectedPhaseSummary = BuildPhaseSummary(item.Phase, UseDarkTheme);
        IsPhaseSummarySheetVisible = true;
    }

    /// <summary>Hides the sheet.</summary>
    private void ClosePhaseSummarySheet()
    {
        IsPhaseSummarySheetVisible = false;
    }

    /// <summary>Closes the sheet and opens the full phase page for the phase it was showing.</summary>
    private async Task OpenSelectedPhaseDetailsAsync()
    {
        if (SelectedPhaseSummary is null)
            return;

        var phase = SelectedPhaseSummary.Phase;
        IsPhaseSummarySheetVisible = false;
        await _openPhaseDetailsAsync(phase);
    }

    /// <summary>Re-derives everything the section shows for a phase — snapshot, tips and pills — so a phase or theme change</summary>
    /// is applied in one place.</summary>
    private void RefreshGuidanceContent(CyclePhase phase, bool useDarkTheme)
    {
        PhaseHormonalSnapshot = GetHormonalSnapshot(phase);
        PhaseEnergyBadgeText = GetPhaseEnergyBadge(phase);
        PhaseVectorArtworkData = GetPhaseVectorPath(phase);
        DailyCheerText = GetDailyCheer(phase);
        DailyTrainingTipText = GetTrainingMicroTip(phase);
        DailyNutritionTipText = GetNutritionMicroTip(phase);
        PhaseExplorerPills = BuildPhaseExplorerPills(phase, useDarkTheme);
    }

    /// <summary>The hormonal snapshot line shown for a phase.</summary>
    public static string GetHormonalSnapshot(CyclePhase phase)
    {
        return PhaseContextText;
    }

    /// <summary>The energy badge shown beside the phase.</summary>
    public static string GetPhaseEnergyBadge(CyclePhase phase)
    {
        return MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Guidance_EnergyBadge");
    }

    /// <summary>The artwork path for a phase, falling back to a neutral circle for an unknown one so the view never draws</summary>
    /// nothing.</summary>
    public static string GetPhaseVectorPath(CyclePhase phase)
    {
        return phase switch
        {
            // Droplet / recovery arc
            CyclePhase.Menstrual => "M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z",
            // Sprouting seedling / leaf
            CyclePhase.Follicular =>
                "M12 22v-8m0 0c0-4 3.5-7 7.5-7 0 4-3.5 7-7.5 7zm0 0c0-3-2.5-5.5-6-5.5 0 3 2.5 5.5 6 5.5z",
            // Radiant sun / peak power
            CyclePhase.Ovulatory =>
                "M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0-5v3m0 14v3m10-10h-3M5 12H2m15.07-7.07l-2.12 2.12M9.05 14.95l-2.12 2.12m0-10.14l2.12 2.12m5.9 5.9l2.12 2.12",
            // Balance moon
            CyclePhase.Luteal => "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z",
            _ => "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z"
        };
    }

    /// <summary>The check-in line for the phase.</summary>
    public static string GetDailyCheer(CyclePhase phase)
    {
        return PhaseCheckInText;
    }

    /// <summary>The training tip for the phase.</summary>
    public static string GetTrainingMicroTip(CyclePhase phase)
    {
        return PhaseTrainingText;
    }

    /// <summary>The nutrition tip for the phase.</summary>
    public static string GetNutritionMicroTip(CyclePhase phase)
    {
        return PhaseNutritionText;
    }

    /// <summary>The phase pills shown in the explorer.</summary>
    private static ObservableCollection<PhaseExplorerPillItem> BuildPhaseExplorerPills(CyclePhase currentPhase,
        bool useDarkTheme)
    {
        return new ObservableCollection<PhaseExplorerPillItem>
        {
            new()
            {
                Phase = CyclePhase.Menstrual,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Name"),
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseExplorerPillColor(CyclePhase.Menstrual, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Menstrual
            },
            new()
            {
                Phase = CyclePhase.Follicular,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Name"),
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseExplorerPillColor(CyclePhase.Follicular, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Follicular
            },
            new()
            {
                Phase = CyclePhase.Ovulatory,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Name"),
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseExplorerPillColor(CyclePhase.Ovulatory, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Ovulatory
            },
            new()
            {
                Phase = CyclePhase.Luteal,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Name"),
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseExplorerPillColor(CyclePhase.Luteal, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Luteal
            }
        };
    }

    /// <summary>The summary sheet's content for a phase, with the phase she is actually in marked so the sheet is not read</summary>
    /// as advice about a phase she is not in.</summary>
    public static PhaseSummarySheetModel BuildPhaseSummary(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Menstrual,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Title"),
                Window = PhaseTimingText,
                Tagline = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Tagline"),
                HormonalSummary = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Hormonal"),
                TrainingFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Training"),
                NutritionFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Nutrition"),
                RecoveryFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_Recovery"),
                QuickRule = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Menstrual_QuickRule"),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default)
            },
            CyclePhase.Follicular => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Follicular,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Title"),
                Window = PhaseTimingText,
                Tagline = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Tagline"),
                HormonalSummary = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Hormonal"),
                TrainingFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Training"),
                NutritionFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Nutrition"),
                RecoveryFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_Recovery"),
                QuickRule = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Follicular_QuickRule"),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default)
            },
            CyclePhase.Ovulatory => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Ovulatory,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Title"),
                Window = PhaseTimingText,
                Tagline = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Tagline"),
                HormonalSummary = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Hormonal"),
                TrainingFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Training"),
                NutritionFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Nutrition"),
                RecoveryFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_Recovery"),
                QuickRule = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Ovulatory_QuickRule"),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default)
            },
            CyclePhase.Luteal => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Luteal,
                Name = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Title"),
                Window = PhaseTimingText,
                Tagline = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Tagline"),
                HormonalSummary = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Hormonal"),
                TrainingFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Training"),
                NutritionFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Nutrition"),
                RecoveryFocus = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_Recovery"),
                QuickRule = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_Luteal_QuickRule"),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default)
            },
            _ => new PhaseSummarySheetModel
            {
                Phase = phase,
                Name = phase.ToString(),
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                HormonalSummary = PhaseContextText,
                TrainingFocus = PhaseTrainingText,
                NutritionFocus = PhaseNutritionText,
                RecoveryFocus = PhaseRecoveryText,
                QuickRule = PhaseCheckInText,
                IconSource = CyclePhaseAssets.GetIconSource(phase),
                BackgroundColor = AppThemeResources.GetRequired<Color>("Transparent"),
                TextColor = AppThemeResources.GetColor(useDarkTheme ? "TextPrimaryDark" : "TextPrimary"),
                AccentColor = AppThemeResources.GetRequired<Color>("Transparent")
            }
        };
    }
}
