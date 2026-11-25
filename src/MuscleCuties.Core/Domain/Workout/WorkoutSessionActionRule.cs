namespace MuscleCuties.Core.Domain.Workout;

/// <summary>One exercise's log inside a finished session.</summary>
public sealed record WorkoutSessionExerciseLog(
    int WorkoutDayExerciseId,
    int ExerciseId,
    int CompletedSets,
    int CompletedReps,
    float? WeightKg);

/// <summary>Applies session actions, refusing one that would leave the session without a completed set.</summary>
public static class WorkoutSessionActionRule
{
    public const int MinimumPositiveIdentifier = 1;
    public const int MinimumNonNegativeCount = 0;
    public const float MinimumWeightKg = 0f;
    public const int MaximumMovementNoteCharacters = 2000;

    /// <summary>True when the identifier is a positive catalog id.</summary>
    public static bool IsValidIdentifier(int identifier) => identifier >= MinimumPositiveIdentifier;

    /// <summary>True when the session log carries plausible sets and effort.</summary>
    public static bool IsValidSessionLog(
        int workoutDayId,
        DateOnly date,
        IEnumerable<WorkoutSessionExerciseLog>? exercises)
    {
        return IsValidIdentifier(workoutDayId) &&
               WorkoutScheduleMathRule.IsSupportedDate(date) &&
               exercises is not null &&
               exercises.All(IsValidExerciseLog);
    }

    /// <summary>True when the swap names a real day exercise and a real replacement.</summary>
    public static bool IsValidSwap(int workoutDayExerciseId, int newExerciseId) =>
        IsValidIdentifier(workoutDayExerciseId) && IsValidIdentifier(newExerciseId);

    /// <summary>True when the note is short enough to store.</summary>
    public static bool IsValidMovementNote(int workoutDayExerciseId, string? notes) =>
        IsValidIdentifier(workoutDayExerciseId) &&
        (notes?.Trim().Length ?? MinimumNonNegativeCount) <= MaximumMovementNoteCharacters;

    /// <summary>True when the optional exercise's set count is allowed.</summary>
    public static bool IsValidOptionalExercise(int workoutDayExerciseId, int optionalSets) =>
        IsValidIdentifier(workoutDayExerciseId) && optionalSets >= MinimumNonNegativeCount;

    /// <summary>Whether a log names a known exercise and a plausible set.</summary>
    private static bool IsValidExerciseLog(WorkoutSessionExerciseLog exercise)
    {
        return IsValidIdentifier(exercise.WorkoutDayExerciseId) &&
               IsValidIdentifier(exercise.ExerciseId) &&
               exercise.CompletedSets >= MinimumNonNegativeCount &&
               exercise.CompletedReps >= MinimumNonNegativeCount &&
               (exercise.WeightKg is not { } weight ||
                (float.IsFinite(weight) && weight >= MinimumWeightKg));
    }
}
