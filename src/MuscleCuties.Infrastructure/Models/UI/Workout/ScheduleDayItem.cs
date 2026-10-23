namespace MuscleCuties.Core.Models.UI.Workout;

/// <summary>
///     Represents one day in the 7-day Mon–Sun cycle-synced schedule strip
///     shown on the WorkoutPage.
/// </summary>
public sealed class ScheduleDayItem
{
    /// <summary>Short day label (MON, TUE, … SUN).</summary>
    public string DayLabel { get; init; } = string.Empty;

    /// <summary>Calendar date for this schedule slot.</summary>
    public DateTime Date { get; init; }

    /// <summary>0-indexed week within the multi-week schedule (0..3).</summary>
    public int WeekIndex { get; init; }

    /// <summary>Workout type tag (STRENGTH, CARDIO, RECOVERY, REST).</summary>
    public string WorkoutTag { get; init; } = string.Empty;

    /// <summary>Workout name for the tooltip / accessibility (e.g. "Upper body push").</summary>
    public string WorkoutTitle { get; init; } = string.Empty;

    /// <summary>WorkoutDayId for navigation. 0 for rest/unassigned days.</summary>
    public int WorkoutDayId { get; init; }

    /// <summary>True when this day is today's date.</summary>
    public bool IsToday { get; init; }

    /// <summary>True when today's workout has been completed.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>True for rest days (no workout).</summary>
    public bool IsRestDay { get; init; }

    /// <summary>Accent color matching the workout type.</summary>
    public string AccentColor { get; init; } = string.Empty;

    /// <summary>Text color matching the workout type.</summary>
    public string AccentTextColor { get; init; } = string.Empty;
}
