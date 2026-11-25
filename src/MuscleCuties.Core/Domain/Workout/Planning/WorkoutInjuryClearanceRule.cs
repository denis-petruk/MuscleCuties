namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>One injury episode, with whether it is still restricting training.</summary>
public sealed record InjuryRestorationEpisode(bool IsActive, DateOnly? ClearedOn);
/// <summary>Whether a displaced slot can be restored now, must wait, or is gone for good.</summary>
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

    // Gate 88: Conservative return load (10-15% reduction -> 0.875x) for the first 2 sessions
    // after explicit pain clearance.
    public const decimal ConservativeReturnLoadMultiplier = 0.875m;
    public const int ConservativeReturnSessionCount = 2;

    /// <summary>The reduced load the injured lift returns at.</summary>
    public static decimal ComputeConservativeReturnLoad(
        decimal priorWorkingLoadKg,
        int completedConservativeSessions,
        decimal incrementKg = 2.5m)
    {
        if (priorWorkingLoadKg <= 0m)
            return 0m;
        if (completedConservativeSessions >= ConservativeReturnSessionCount)
            return priorWorkingLoadKg;

        var step = incrementKg > 0m ? incrementKg : 2.5m;
        var rawReduced = priorWorkingLoadKg * ConservativeReturnLoadMultiplier;
        var snapped = Math.Floor(rawReduced / step) * step;
        return snapped > 0m ? snapped : rawReduced;
    }

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

    /// <summary>The first day a restoration may run, or null when nothing is required.</summary>
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

    /// <summary>True when the restored exercise must stay optional for the week.</summary>
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

    /// <summary>True when every injured site has cleared, so normal loading can resume.</summary>
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
