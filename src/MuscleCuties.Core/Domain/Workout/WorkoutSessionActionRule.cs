namespace MuscleCuties.Core.Domain.Workout;

public sealed record WorkoutSessionExerciseLog(
    int WorkoutDayExerciseId,
    int ExerciseId,
    int CompletedSets,
    int CompletedReps,
    float? WeightKg);

public static class WorkoutSessionActionRule
{
    public const int MinimumPositiveIdentifier = 1;
    public const int MinimumNonNegativeCount = 0;
    public const float MinimumWeightKg = 0f;
    public const int MaximumMovementNoteCharacters = 2000;

    public static bool IsValidIdentifier(int identifier) => identifier >= MinimumPositiveIdentifier;

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

    public static bool IsValidSwap(int workoutDayExerciseId, int newExerciseId) =>
        IsValidIdentifier(workoutDayExerciseId) && IsValidIdentifier(newExerciseId);

    public static bool IsValidMovementNote(int workoutDayExerciseId, string? notes) =>
        IsValidIdentifier(workoutDayExerciseId) &&
        (notes?.Trim().Length ?? MinimumNonNegativeCount) <= MaximumMovementNoteCharacters;

    public static bool IsValidOptionalExercise(int workoutDayExerciseId, int optionalSets) =>
        IsValidIdentifier(workoutDayExerciseId) && optionalSets >= MinimumNonNegativeCount;

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
