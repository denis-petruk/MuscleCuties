using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>
/// The one tag-to-icon mapping for workout surfaces. Tag values are stable activity codes,
/// not display copy, so the same movement type carries the same visual everywhere.
/// </summary>
public static class WorkoutActivityIcons
{
    /// <summary>The per-activity glyph a planned session row shows. Planned activities never share a
    /// glyph (rest and recovery deliberately share the moon), and an activity the app cannot name
    /// gets a neutral marker instead of a strength icon, so a week of different sessions never reads
    /// as one repeated picture.</summary>
    public static string ForActivity(int activityTypeId) =>
        Enum.IsDefined(typeof(WorkoutActivityType), activityTypeId)
            ? (WorkoutActivityType)activityTypeId switch
            {
                WorkoutActivityType.StrengthHighIntensity => "Flash24",
                WorkoutActivityType.HighVolumeStrength => "Dumbbell24",
                WorkoutActivityType.RockClimbing => "Sport24",
                WorkoutActivityType.Yoga => "LeafThree24",
                WorkoutActivityType.Hiit => "Fire24",
                WorkoutActivityType.Cycling => "Bicycle24",
                WorkoutActivityType.Running => "Run24",
                WorkoutActivityType.Swimming => "SwimmingPool24",
                WorkoutActivityType.GeneralStrength => "Trophy24",
                WorkoutActivityType.Walking => "People24",
                WorkoutActivityType.Rowing => "PulseSquare24",
                WorkoutActivityType.Elliptical => "ArrowSync24",
                WorkoutActivityType.Mobility => "ArrowTrending24",
                WorkoutActivityType.GentleRecovery => "WeatherMoon24",
                WorkoutActivityType.RestDay => "WeatherMoon24",
                _ => "CircleSmall24"
            }
            : "CircleSmall24";

    public static string ForTag(string? tag) => (tag ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "STRENGTH" => "Dumbbell24",
        "CARDIO" => "PulseSquare24",
        "RECOVERY" => "LeafThree24",
        "REST" => "WeatherMoon24",
        "CLIMBING" => "Sport24",
        "YOGA" => "LeafThree24",
        _ => "Sport24"
    };
}
