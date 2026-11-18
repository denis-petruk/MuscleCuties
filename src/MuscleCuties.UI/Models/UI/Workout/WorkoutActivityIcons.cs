namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>
/// The one tag-to-icon mapping for workout surfaces. Tag values are stable activity codes,
/// not display copy, so the same movement type carries the same visual everywhere.
/// </summary>
public static class WorkoutActivityIcons
{
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
