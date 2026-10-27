namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Finds the first selected local workout day eligible for a new plan.</summary>
public static class WorkoutPlanStartDateRule
{
    public const int DaysPerWeek = 7;
    public const int TodayOffsetDays = 0;

    /// <summary>Returns null for missing or invalid inputs or an unsupported date range.</summary>
    public static DateOnly? FindNextEligibleDate(
        DateTimeOffset now,
        TimeZoneInfo? accountTimeZone,
        IReadOnlyCollection<DayOfWeek>? selectedDays,
        bool hasCompletedActivityToday)
    {
        if (accountTimeZone is null || selectedDays is null || selectedDays.Count == 0 ||
            selectedDays.Any(day => !Enum.IsDefined(day)))
            return null;

        var localDate = WorkoutAccountCalendarRule.LocalDate(now, accountTimeZone);
        for (var offset = TodayOffsetDays; offset <= DaysPerWeek; offset++)
        {
            if (offset == TodayOffsetDays && hasCompletedActivityToday)
                continue;
            if (localDate.DayNumber > DateOnly.MaxValue.DayNumber - offset)
                return null;
            var candidate = localDate.AddDays(offset);
            if (selectedDays.Contains(candidate.DayOfWeek))
                return candidate;
        }

        return null;
    }
}
