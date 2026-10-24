using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;

namespace MuscleCuties.UI.ViewModels.Cycle;

public partial class CycleViewModel
{
    private const string PhaseContextText =
        "A cycle phase is context, not a measurement of hormone levels or today's exercise capacity.";
    private const string PhaseCheckInText =
        "Notice how you feel today and record any symptoms that matter to you.";
    private const string PhaseTrainingText =
        "Use your current check-in and restrictions when reviewing your plan. A phase label alone does not set training intensity.";
    private const string PhaseNutritionText =
        "Follow your personal nutrition goals. A phase label alone does not change calorie or macro targets.";
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
    public AsyncRelayCommand OpenFullPhaseGuideCommand { get; private set; } = null!;
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Cycle XAML; keep the unreviewed guide unavailable to this view model.")]
    public bool IsPhaseGuideAvailable => false;

    private void InitializeGuidanceCommands()
    {
        SelectPhaseExplorerPillCommand = new RelayCommand<PhaseExplorerPillItem>(SelectPhaseExplorerPill);
        ClosePhaseSummarySheetCommand = new RelayCommand(ClosePhaseSummarySheet);
        OpenFullPhaseGuideCommand = new AsyncRelayCommand(OpenFullPhaseGuideAsync);

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

    private async Task OpenFullPhaseGuideAsync()
    {
        if (!IsPhaseGuideAvailable || SelectedPhaseSummary is null) return;
        var phase = SelectedPhaseSummary.Phase;
        IsPhaseSummarySheetVisible = false;
        await _openPhaseDetailsAsync(phase);
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
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, Colors.Red),
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
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, Colors.Green),
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
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Orange),
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
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, Colors.Purple),
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
                Tagline = PhaseExplorerTagline,
                HormonalSummary = PhaseContextText,
                TrainingFocus = PhaseTrainingText,
                NutritionFocus = PhaseNutritionText,
                RecoveryFocus = PhaseRecoveryText,
                QuickRule = PhaseCheckInText,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme, Colors.Red)
            },
            CyclePhase.Follicular => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular Phase",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                HormonalSummary = PhaseContextText,
                TrainingFocus = PhaseTrainingText,
                NutritionFocus = PhaseNutritionText,
                RecoveryFocus = PhaseRecoveryText,
                QuickRule = PhaseCheckInText,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme, Colors.Green)
            },
            CyclePhase.Ovulatory => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory Phase",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                HormonalSummary = PhaseContextText,
                TrainingFocus = PhaseTrainingText,
                NutritionFocus = PhaseNutritionText,
                RecoveryFocus = PhaseRecoveryText,
                QuickRule = PhaseCheckInText,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme, Colors.Orange)
            },
            CyclePhase.Luteal => new PhaseSummarySheetModel
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal Phase",
                Window = PhaseTimingText,
                Tagline = PhaseExplorerTagline,
                HormonalSummary = PhaseContextText,
                TrainingFocus = PhaseTrainingText,
                NutritionFocus = PhaseNutritionText,
                RecoveryFocus = PhaseRecoveryText,
                QuickRule = PhaseCheckInText,
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                BackgroundColor =
                    PhaseThemePalette.GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme, Colors.Transparent),
                TextColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, Colors.Black),
                AccentColor = PhaseThemePalette.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme, Colors.Purple)
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
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Black,
                AccentColor = Colors.Gray
            }
        };
    }
}
