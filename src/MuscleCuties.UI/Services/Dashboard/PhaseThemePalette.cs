using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Services.Dashboard;

internal static class PhaseThemePalette
{
    public static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme, Color fallback)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#482B33" : "#F9D6D8"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#243B27" : "#D6EED6"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#3E3218" : "#FFF0C4"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#362548" : "#E8D8F5"),
            _ => fallback
        };
    }

    public static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme, Color fallback)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#7A3A48"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#3A6B3A"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#7A6000"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#5A3B80"),
            _ => fallback
        };
    }

    public static Color GetPhaseDividerColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#7A4A54" : "#E8B7BE"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#447146" : "#B8D9B8"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#776516" : "#E6CF88"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#5A3F78" : "#D4BCEB"),
            _ => GetNeutralDividerColor(useDarkTheme)
        };
    }

    public static Color GetPredictedDayStrokeColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#B86B78"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#679B67"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#B08A00"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#8B6AB8"),
            _ => Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#8B7E86")
        };
    }

    public static Color GetNeutralCardBackgroundColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#3A2931" : "#FFF1F6");
    }

    public static Color GetNeutralCardTextColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#5B4650");
    }

    public static Color GetNeutralDividerColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#5A424C" : "#EBD3DE");
    }

    public static Color GetSelectedDayBackgroundColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#4A3240" : "#F6DDE6");
    }

    public static Color GetSelectedDayStrokeColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#F0B8CF" : "#A6446D");
    }
}
