using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UI.Services.Dashboard;

public static class MacroProgressFormatter
{
    public static float CalculateProgress(float consumed, float target)
    {
        return target <= 0f ? 0f : Math.Clamp(consumed / target, 0f, 1f);
    }

    public static string FormatCaloriesConsumed(float consumed)
    {
        return $"{(int)consumed:N0} kcal";
    }

    public static string FormatCaloriesGoal(float target)
    {
        return $"/ {(int)target:N0} kcal";
    }

    public static string FormatGramsText(float consumed, float target)
    {
        return $"{(int)consumed}g / {(int)target}g";
    }

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

    public static void NotifyCaloriesConsumed(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CaloriesProgress");
        onPropertyChanged("CaloriesConsumed");
    }

    public static void NotifyCaloriesGoal(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CaloriesProgress");
        onPropertyChanged("CaloriesGoal");
    }

    public static void NotifyProtein(Action<string> onPropertyChanged)
    {
        onPropertyChanged("ProteinText");
        onPropertyChanged("ProteinProgress");
    }

    public static void NotifyCarbs(Action<string> onPropertyChanged)
    {
        onPropertyChanged("CarbsText");
        onPropertyChanged("CarbsProgress");
    }

    public static void NotifyFats(Action<string> onPropertyChanged)
    {
        onPropertyChanged("FatsText");
        onPropertyChanged("FatsProgress");
    }
}
