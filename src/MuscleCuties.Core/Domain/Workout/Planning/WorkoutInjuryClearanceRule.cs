namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record InjuryRestorationEpisode(bool IsActive, DateOnly? ClearedOn);
public enum InjuryClearanceRestorationDisposition { Blocked, RestoreOptional, RestoreRequired }

/// <summary>
/// S-05 (2026-11-09, owner ratified Option A): no fixed post-clearance window. No primary source supports a
/// seven-day optional period for healthy adult trainees, and the preview never showed it. A cleared exercise is
/// restored as prescribed from its clearance date, and only when current eligibility permits; unknown or future
/// clearance still blocks. The 0-day window is the ratified policy; the constant is kept only so legacy rows and the persisted vocabulary stay readable.
/// </summary>
public static class WorkoutInjuryClearanceRule
{
    public const int OptionalCalendarDaysIncludingClearance = 0;

    /// <summary>All related episodes and current eligibility must be known before restoration.</summary>
    public static InjuryClearanceRestorationDisposition EvaluateRestoration(bool originalWasOptional,
        bool currentEligibilityConfirmed, IReadOnlyCollection<InjuryRestorationEpisode>? episodes,
        DateOnly effectiveDate)
    {
        if (!currentEligibilityConfirmed || episodes is null || episodes.Count == 0 ||
            episodes.Any(episode => episode.IsActive || !CanRestore(true, episode.ClearedOn, effectiveDate)))
            return InjuryClearanceRestorationDisposition.Blocked;
        if (originalWasOptional || episodes.Any(episode => IsRestoredExerciseOptional(episode.ClearedOn!.Value, effectiveDate)))
            return InjuryClearanceRestorationDisposition.RestoreOptional;
        return InjuryClearanceRestorationDisposition.RestoreRequired;
    }

    /// <summary>Keep restriction when clearance or the effective local date is not proven.</summary>
    public static bool RequiresConservativeRestriction(bool isCleared, DateOnly? clearedOn, DateOnly? effectiveDate) =>
        !isCleared || clearedOn is null || effectiveDate is null || clearedOn > effectiveDate ||
        IsRestoredExerciseOptional(clearedOn.Value, effectiveDate.Value);

    public static DateOnly? RequiredFromLocalDate(DateOnly? originalClearanceDate)
    {
        if (originalClearanceDate is not { } clearance ||
            clearance.DayNumber > DateOnly.MaxValue.DayNumber - OptionalCalendarDaysIncludingClearance)
            return null;
        return clearance.AddDays(OptionalCalendarDaysIncludingClearance);
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
