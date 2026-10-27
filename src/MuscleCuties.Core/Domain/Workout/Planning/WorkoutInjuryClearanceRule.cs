namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Restoration never resets the window anchored to the original clearance date.</summary>
public static class WorkoutInjuryClearanceRule
{
    public const int FullCalendarDaysAfterClearance = 7;
    public const int FollowingMidnightOffsetDays = FullCalendarDaysAfterClearance + 1;

    public static DateOnly? RequiredFromLocalDate(DateOnly? originalClearanceDate)
    {
        if (originalClearanceDate is not { } clearance ||
            clearance.DayNumber > DateOnly.MaxValue.DayNumber - FollowingMidnightOffsetDays)
            return null;
        return clearance.AddDays(FollowingMidnightOffsetDays);
    }

    /// <summary>Unknown clearance provenance blocks restoration rather than inventing a deadline.</summary>
    public static bool CanRestore(bool allCurrentRestrictionsPermit, DateOnly? originalClearanceDate,
        DateOnly effectiveDate) =>
        allCurrentRestrictionsPermit && originalClearanceDate is { } clearance &&
        clearance <= effectiveDate && RequiredFromLocalDate(clearance).HasValue;

    public static bool IsRestoredExerciseOptional(DateOnly originalClearanceDate, DateOnly effectiveDate) =>
        RequiredFromLocalDate(originalClearanceDate) is not { } requiredFrom || effectiveDate < requiredFrom;
}
