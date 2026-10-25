using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;

namespace MuscleCuties.UI.ViewModels.Cycle;

public partial class CycleViewModel
{
    private const string PhaseContextText =
        "A cycle phase is context, not a measurement of hormone levels or today's exercise capacity.";
    private const string PhaseCheckInText =
        "Notice how you feel today.";
    private const string PhaseTrainingText =
        "Let your check-in and restrictions guide training.";
    private const string PhaseNutritionText =
        "Follow your personal nutrition goals.";
    private const string PhaseRecoveryText =
        "Rest and activity needs vary. Use your own symptoms and preferences as context.";
    private const string PhaseTimingText = "Timing varies";
    private const string PhaseExplorerTagline = "Track your experience";
    [ObservableProperty] private string _dailyCheerText = string.Empty;
    [ObservableProperty] private string _dailyNutritionTipText = string.Empty;
    [ObservableProperty] private string _dailyTrainingTipText = string.Empty;
    [ObservableProperty] private bool _isPhaseSummarySheetVisible;
    [ObservableProperty] private string _phaseEnergyBadgeText = string.Empty;
    [ObservableProperty] private ObservableCollection<PhaseExplorerPillItem> _phaseExplorerPills = new();
    [ObservableProperty] private string _phaseHormonalSnapshot = string.Empty;
    [ObservableProperty] private string _phaseVectorArtworkData = string.Empty;
    [ObservableProperty] private PhaseSummarySheetModel? _selectedPhaseSummary;

    public RelayCommand<PhaseExplorerPillItem> SelectPhaseExplorerPillCommand { get; private set; } = null!;
    public RelayCommand ClosePhaseSummarySheetCommand { get; private set; } = null!;

    private void InitializeGuidanceCommands()
    {
        SelectPhaseExplorerPillCommand = new RelayCommand<PhaseExplorerPillItem>(SelectPhaseExplorerPill);
        ClosePhaseSummarySheetCommand = new RelayCommand(ClosePhaseSummarySheet);

        RefreshGuidanceContent(CurrentPhase, UseDarkTheme);
    }

    private void SelectPhaseExplorerPill(PhaseExplorerPillItem? item)
    {
        if (item is null) return;
        SelectedPhaseSummary = BuildPhaseSummary(item.Phase, UseDarkTheme);
        IsPhaseSummarySheetVisible = true;
    }

    private void ClosePhaseSummarySheet()
    {
        IsPhaseSummarySheetVisible = false;
    }

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

    public static string GetHormonalSnapshot(CyclePhase phase)
    {
        return PhaseContextText;
    }

    public static string GetPhaseEnergyBadge(CyclePhase phase)
    {
        return "Cycle context";
    }

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

    public static string GetDailyCheer(CyclePhase phase)
    {
        return PhaseCheckInText;
    }

    public static string GetTrainingMicroTip(CyclePhase phase)
    {
        return PhaseTrainingText;
    }

    public static string GetNutritionMicroTip(CyclePhase phase)
    {
        return PhaseNutritionText;
    }

    private static ObservableCollection<PhaseExplorerPillItem> BuildPhaseExplorerPills(CyclePhase currentPhase,
        bool useDarkTheme)
    {
        return new ObservableCollection<PhaseExplorerPillItem>
        {
            new()
            {
                Phase = CyclePhase.Menstrual,
                Name = "Menstrual",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Menstrual
            },
            new()
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Follicular
            },
            new()
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Ovulatory
            },
            new()
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, default),
                IsActivePhase = currentPhase == CyclePhase.Luteal
            }
        };
    }

    public static PhaseSummarySheetModel BuildPhaseSummary(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Menstrual,
                Name = "Menstrual Phase",
                Window = PhaseTimingText,
                Tagline = "Period begins",
                HormonalSummary = "Bleeding starts a new cycle. Cramps or fatigue may occur, but experiences vary.",
                TrainingFocus = "Choose effort from today's check-in. Adjust or rest if symptoms interfere.",
                NutritionFocus = "Follow your nutrition goals, with meals and fluids that feel comfortable.",
                RecoveryFocus = "Notice pain or fatigue and make room for rest if needed.",
                QuickRule = "Log bleeding and symptoms to learn your pattern.",
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, default)
            },
            CyclePhase.Follicular => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular Phase",
                Window = PhaseTimingText,
                Tagline = "Before ovulation",
                HormonalSummary = "Follicles develop and the uterine lining rebuilds. Timing varies.",
                TrainingFocus = "Follow your plan and adjust effort to today's energy and restrictions.",
                NutritionFocus = "Keep your personal nutrition goals; the phase alone does not change targets.",
                RecoveryFocus = "Track how you feel as the cycle progresses.",
                QuickRule = "Your check-in matters more than an estimated phase.",
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, default)
            },
            CyclePhase.Ovulatory => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory Phase",
                Window = PhaseTimingText,
                Tagline = "Around ovulation",
                HormonalSummary = "An egg may be released in this window. A calendar estimate cannot confirm ovulation.",
                TrainingFocus = "Use your planned session, adjusting for symptoms or restrictions.",
                NutritionFocus = "Follow your usual nutrition goals and appetite cues.",
                RecoveryFocus = "Record any changes you notice; they may differ from cycle to cycle.",
                QuickRule = "Treat this as an estimate, not a fertility signal.",
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme, default),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, default)
            },
            CyclePhase.Luteal => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal Phase",
                Window = PhaseTimingText,
                Tagline = "After ovulation",
                HormonalSummary = "Progesterone usually rises after ovulation. Premenstrual symptoms vary between people and cycles.",
                TrainingFocus = "Keep your plan flexible if symptoms affect comfort.",
                NutritionFocus = "Follow your goals; if appetite changes, choose satisfying meals.",
                RecoveryFocus = "Make room for sleep and recovery when you need it.",
                QuickRule = "Record symptoms instead of assuming the phase predicts how you feel.",
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
