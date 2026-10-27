namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record InjuryRestorationEpisode(bool IsActive, DateOnly? ClearedOn);
public enum InjuryRestorationDisposition { Blocked, RestoreOptional, RestoreRequired }

/// <summary>Restoration never resets the window anchored to the original clearance date.</summary>
public static class WorkoutInjuryClearanceRule
{
    public const int FullCalendarDaysAfterClearance = 7;
    public const int FollowingMidnightOffsetDays = FullCalendarDaysAfterClearance + 1;

    /// <summary>All related episodes and current eligibility must be known before restoration.</summary>
    public static InjuryRestorationDisposition EvaluateRestoration(bool originalWasOptional,
        bool currentEligibilityConfirmed, IReadOnlyCollection<InjuryRestorationEpisode>? episodes,
        DateOnly effectiveDate)
    {
        if (!currentEligibilityConfirmed || episodes is null || episodes.Count == 0 ||
            episodes.Any(episode => episode.IsActive || !CanRestore(true, episode.ClearedOn, effectiveDate)))
            return InjuryRestorationDisposition.Blocked;
        if (originalWasOptional || episodes.Any(episode => IsRestoredExerciseOptional(episode.ClearedOn!.Value, effectiveDate)))
            return InjuryRestorationDisposition.RestoreOptional;
        return InjuryRestorationDisposition.RestoreRequired;
    }

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

    /// <summary>Each injury affecting the original exercise must have an explicit clearance.
    /// The latest clearance controls the window; delayed reconciliation never moves it.</summary>
    public static DateOnly? RequiredFromForAllClearances(IEnumerable<DateOnly?> clearanceDates)
    {
        ArgumentNullException.ThrowIfNull(clearanceDates);
        var dates = clearanceDates.ToArray();
        if (dates.Length == 0 || dates.Any(date => !date.HasValue)) return null;
        return RequiredFromLocalDate(dates.Max(date => date!.Value));
    }

    public static bool CanRestoreForAllClearances(IEnumerable<DateOnly?> clearanceDates,
        bool allCurrentConstraintsPermit, DateOnly effectiveDate)
    {
        ArgumentNullException.ThrowIfNull(clearanceDates);
        var dates = clearanceDates.ToArray();
        return allCurrentConstraintsPermit && dates.Length > 0 &&
            dates.All(date => date.HasValue && date.Value <= effectiveDate) &&
            RequiredFromForAllClearances(dates).HasValue;
    }
}
