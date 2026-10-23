namespace MuscleCuties.UseCases.Features.Workout.Injury;

public enum WorkoutInjurySite
{
    None = 0,
    Foot = 1,
    Knee = 2,
    Ankle = 4,
    Shoulder = 8,
    LowBack = 16,
    Wrist = 32,
    Hip = 64,
    Neck = 128
}

public enum WorkoutInjuryStatus
{
    Acute = 0,
    Recovering = 1,
    Cleared = 2
}

public sealed record WorkoutInjuryLogDto(
    int Id,
    WorkoutInjurySite Site,
    WorkoutInjuryStatus Status,
    int Pain,
    DateOnly Since,
    DateOnly LoggedDate,
    bool IsActive);

public sealed record SaveWorkoutInjuryInput(
    int Id,
    WorkoutInjurySite Site,
    WorkoutInjuryStatus Status,
    int Pain,
    DateOnly Since);

public sealed record WorkoutInjuryImpactDto(
    bool HasActiveInjuries,
    string Summary,
    IReadOnlyList<string> BlockedExercises,
    IReadOnlyList<string> RehabExercises,
    IReadOnlyList<string> BlockedActivities);

public sealed record ExerciseContraindicationCheckDto(
    int ExerciseDefinitionId,
    string ExerciseName,
    bool IsContraindicated,
    IReadOnlyList<string> ActiveInjurySites,
    string? Reason);

public sealed record SwapExerciseRequest(
    Guid WorkoutDayExerciseId,
    int NewExerciseDefinitionId,
    bool SavePreference);

public sealed record SwapExerciseResponse(
    bool Success,
    string Message);
