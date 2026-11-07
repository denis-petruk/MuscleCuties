using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Session.Contracts;

/// <summary>One editable set row from the session UI.</summary>
public sealed record EditableObservedSet(
    int SetNumber,
    int RepsCompleted,
    decimal LoadKg,
    int? RirActual,
    bool TechniqueAcceptable = true,
    bool IsWarmup = false);

public static class ObservedSetInputMapping
{
    /// <summary>
    /// Returns null when the user did not edit the set rows, preserving the legacy aggregate
    /// payload. Edited rows are filtered to working sets and renumbered from one.
    /// </summary>
    public static IReadOnlyList<ObservedSetRecord>? Build(
        bool rowsEdited,
        IEnumerable<EditableObservedSet> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (!rowsEdited) return null;

        var observed = rows
            .Where(row => !row.IsWarmup && row.RepsCompleted > 0)
            .OrderBy(row => row.SetNumber)
            .Select((row, index) => new ObservedSetRecord(
                index + 1,
                row.RepsCompleted,
                row.LoadKg,
                row.RirActual,
                row.TechniqueAcceptable))
            .ToArray();

        return observed.Length > 0 ? observed : null;
    }
}

public static class TimedActivityActualFactory
{
    /// <summary>
    /// Builds the timed-only activity actual. The session finish endpoint records duration;
    /// this payload is what marks the row-less activity complete without inventing sets.
    /// </summary>
    public static ActivityActualLogDto Build(
        WorkoutActivityDto activityType,
        int durationSeconds,
        int? effortRpe = null,
        string? notes = null) =>
        new(
            WorkoutDayActivityId: null,
            ActivityType: activityType,
            DurationSeconds: Math.Max(0, durationSeconds),
            EffortRpe: effortRpe,
            Notes: notes);
}
