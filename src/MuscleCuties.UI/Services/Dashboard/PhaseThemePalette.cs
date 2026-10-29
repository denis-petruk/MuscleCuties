using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Services.Dashboard;

internal static class PhaseThemePalette
{
    public static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme, Color? fallback = null)
    {
        return phase switch
        {
            CyclePhase.Menstrual => GetThemeColor(useDarkTheme, "CyclePhaseMenstrualLight", "PhaseThemeMenstrualBackgroundDark", fallback),
            CyclePhase.Follicular => GetThemeColor(useDarkTheme, "CyclePhaseFollicularLight", "PhaseThemeFollicularBackgroundDark", fallback),
            CyclePhase.Ovulatory => GetThemeColor(useDarkTheme, "CyclePhaseOvulatoryLight", "PhaseThemeOvulatoryBackgroundDark", fallback),
            CyclePhase.Luteal => GetThemeColor(useDarkTheme, "CyclePhaseLutealLight", "PhaseThemeLutealBackgroundDark", fallback),
            _ => fallback ?? GetThemeColor(useDarkTheme, "CardSurface", "CardSurfaceDark")
        };
    }

    /// <summary>
    /// Fill for the stacked phase rows in the Cycle explorer. Dark theme uses a muted tint that sits close to
    /// the card surface: the explorer shows all four phases at once, so full-strength phase fills compete with
    /// the labels. Light theme reuses the existing pastel fills, which are already low contrast on white.
    /// </summary>
    public static Color GetPhaseExplorerPillColor(CyclePhase phase, bool useDarkTheme, Color? fallback = null)
    {
        return phase switch
        {
            CyclePhase.Menstrual => GetThemeColor(useDarkTheme, "CyclePhaseMenstrualLight", "PhaseThemeMenstrualPillDark", fallback),
            CyclePhase.Follicular => GetThemeColor(useDarkTheme, "CyclePhaseFollicularLight", "PhaseThemeFollicularPillDark", fallback),
            CyclePhase.Ovulatory => GetThemeColor(useDarkTheme, "CyclePhaseOvulatoryLight", "PhaseThemeOvulatoryPillDark", fallback),
            CyclePhase.Luteal => GetThemeColor(useDarkTheme, "CyclePhaseLutealLight", "PhaseThemeLutealPillDark", fallback),
            _ => fallback ?? GetNeutralCardBackgroundColor(useDarkTheme)
        };
    }

    public static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme, Color? fallback = null)
    {
        return phase switch
        {
            CyclePhase.Menstrual => GetThemeColor(useDarkTheme, "CyclePhaseMenstrualTextLight", "CyclePhaseMenstrualTextDark", fallback),
            CyclePhase.Follicular => GetThemeColor(useDarkTheme, "CyclePhaseFollicularTextLight", "CyclePhaseFollicularTextDark", fallback),
            CyclePhase.Ovulatory => GetThemeColor(useDarkTheme, "CyclePhaseOvulatoryTextLight", "CyclePhaseOvulatoryTextDark", fallback),
            CyclePhase.Luteal => GetThemeColor(useDarkTheme, "CyclePhaseLutealTextLight", "CyclePhaseLutealTextDark", fallback),
            _ => fallback ?? GetThemeColor(useDarkTheme, "CycleFallbackTextLight", "TextPrimaryDark")
        };
    }

    public static Color GetPhaseDividerColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => GetThemeColor(useDarkTheme, "PhaseThemeMenstrualDividerLight", "PhaseThemeMenstrualDividerDark", default),
            CyclePhase.Follicular => GetThemeColor(useDarkTheme, "PhaseThemeFollicularDividerLight", "PhaseThemeFollicularDividerDark", default),
            CyclePhase.Ovulatory => GetThemeColor(useDarkTheme, "PhaseThemeOvulatoryDividerLight", "PhaseThemeOvulatoryDividerDark", default),
            CyclePhase.Luteal => GetThemeColor(useDarkTheme, "PhaseThemeLutealDividerLight", "PhaseThemeLutealDividerDark", default),
            _ => GetNeutralDividerColor(useDarkTheme)
        };
    }

    public static Color GetPredictedDayStrokeColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => GetThemeColor(useDarkTheme, "PhaseThemeMenstrualPredictionLight", "CyclePhaseMenstrualTextDark", default),
            CyclePhase.Follicular => GetThemeColor(useDarkTheme, "PhaseThemeFollicularPredictionLight", "CyclePhaseFollicularTextDark", default),
            CyclePhase.Ovulatory => GetThemeColor(useDarkTheme, "PhaseThemeOvulatoryPredictionLight", "CyclePhaseOvulatoryTextDark", default),
            CyclePhase.Luteal => GetThemeColor(useDarkTheme, "PhaseThemeLutealPredictionLight", "CyclePhaseLutealTextDark", default),
            _ => GetThemeColor(useDarkTheme, "CycleNeutralTextLight", "TextPrimaryDark", default)
        };
    }

    public static Color GetNeutralCardBackgroundColor(bool useDarkTheme)
    {
        return GetThemeColor(useDarkTheme, "AccentSurface", "CardSurfaceDark", default);
    }

    public static Color GetNeutralCardTextColor(bool useDarkTheme)
    {
        return GetThemeColor(useDarkTheme, "DeepMauve", "TextPrimaryDark", default);
    }

    public static Color GetNeutralDividerColor(bool useDarkTheme)
    {
        return GetThemeColor(useDarkTheme, "InputStroke", "InputStrokeDark", default);
    }

    public static Color GetSelectedDayBackgroundColor(bool useDarkTheme)
    {
        return GetThemeColor(useDarkTheme, "PhaseThemeSelectedDaySurfaceLight", "PhaseThemeSelectedDaySurfaceDark", default);
    }

    public static Color GetSelectedDayStrokeColor(bool useDarkTheme)
    {
        return AppThemeResources.GetColor(useDarkTheme ? "ChoiceSelectedStrokeDark" : "ChoiceSelectedStrokeLight", default);
    }

    private static Color GetThemeColor(bool useDarkTheme, string lightKey, string darkKey, Color? fallback = null)
    {
        return AppThemeResources.GetColor(useDarkTheme ? darkKey : lightKey, fallback);
    }
}
