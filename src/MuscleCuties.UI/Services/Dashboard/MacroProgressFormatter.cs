
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Services.Dashboard;

/// <summary>Shared dashboard formatting for calorie and macro rings, plus the property names each edit</summary>
/// invalidates, so a value and its progress bar never disagree.</summary>
public static class MacroProgressFormatter
{
    /// <summary>Fraction of a target met, clamped to 0..1; a missing target reports 0 rather than dividing by zero.</summary>
    public static float CalculateProgress(float consumed, float target)
    {
        return target <= 0f ? 0f : Math.Clamp(consumed / target, 0f, 1f);
    }

    /// <summary>Calories eaten, rounded to a whole number for the ring label.</summary>
    public static string FormatCaloriesConsumed(float consumed)
    {
        return DisplayFormat.Calories((int)consumed);
    }

    /// <summary>The target as the right-hand half of the "eaten / goal" pair.</summary>
    public static string FormatCaloriesGoal(float target)
    {
        return $"/ {DisplayFormat.Calories((int)target)}";
    }

    /// <summary>Macro grams as "consumed / target", both rounded for the compact row.</summary>
    public static string FormatGramsText(float consumed, float target)
    {
        return $"{DisplayFormat.GramsWholeShort(consumed)} / {DisplayFormat.GramsWholeShort(target)}";
    }

    /// <summary>Phase glyph used beside the cycle card on the dashboard.</summary>
    public static string GetPhaseIconGlyph(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => "Drop24",
            CyclePhase.Follicular => "LeafThree24",
            CyclePhase.Ovulatory => "Fire24",
            CyclePhase.Luteal => "WeatherMoon24",
            _ => "HeartCircle24"
        };
    }

    /// <summary>Raises every calorie and macro property, for a load that replaced all of them at once.</summary>
    public static void NotifyAll(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CaloriesProgress");
        onPropertyChanged("CaloriesConsumed");
        onPropertyChanged("CaloriesGoal");
        onPropertyChanged("ProteinText");
        onPropertyChanged("ProteinProgress");
        onPropertyChanged("CarbsText");
        onPropertyChanged("CarbsProgress");
        onPropertyChanged("FatsText");
        onPropertyChanged("FatsProgress");
    }

    /// <summary>Raises the eaten calories and the ring progress that depends on them.</summary>
    public static void NotifyCaloriesConsumed(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CaloriesProgress");
        onPropertyChanged("CaloriesConsumed");
    }

    /// <summary>Raises the calorie goal and the ring progress that depends on it.</summary>
    public static void NotifyCaloriesGoal(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CaloriesProgress");
        onPropertyChanged("CaloriesGoal");
    }

    /// <summary>Raises the protein text and its bar.</summary>
    public static void NotifyProtein(Action<string> onPropertyChanged)
    {
        onPropertyChanged("ProteinText");
        onPropertyChanged("ProteinProgress");
    }

    /// <summary>Raises the carbs text and its bar.</summary>
    public static void NotifyCarbs(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CarbsText");
        onPropertyChanged("CarbsProgress");
    }

    /// <summary>Raises the fat text and its bar.</summary>
    public static void NotifyFats(Action<string> onPropertyChanged)
    {
        onPropertyChanged("FatsText");
        onPropertyChanged("FatsProgress");
    }
}
