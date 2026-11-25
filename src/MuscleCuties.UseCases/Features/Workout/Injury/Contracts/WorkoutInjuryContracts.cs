namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>Where the injury is, which decides the exercises it blocks.</summary>
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

/// <summary>How the injury stands, which decides whether planning still avoids it.</summary>
public enum WorkoutInjuryStatus
{
    Acute = 0,
    Recovering = 1,
    Cleared = 2
}

/// <summary>One injury as the screen shows it.</summary>
public sealed record WorkoutInjuryLogDto(
    int Id,
    WorkoutInjurySite Site,
    WorkoutInjuryStatus Status,
    int Pain,
    DateOnly Since,
    DateOnly LoggedDate,
    bool IsActive,
    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger =
        MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
    bool? ClinicianAdvisedAvoid = null,
    DateTimeOffset? PainReassessmentDueAtUtc = null);

/// <summary>An injury report sent up.</summary>
public sealed record SaveWorkoutInjuryInput(
    int Id,
    WorkoutInjurySite Site,
    WorkoutInjuryStatus Status,
    int Pain,
    DateOnly Since,
    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger =
        MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
    bool? ClinicianAdvisedAvoid = null);

/// <summary>What an injury currently blocks in the plan.</summary>
public sealed record WorkoutInjuryImpactDto(
    bool HasActiveInjuries,
    string Summary,
    IReadOnlyList<string> BlockedExercises,
    IReadOnlyList<string> RehabExercises,
    IReadOnlyList<string> BlockedActivities);

/// <summary>Whether an exercise is safe given the recorded injuries.</summary>
public sealed record ExerciseContraindicationCheckDto(
    int ExerciseDefinitionId,
    string ExerciseName,
    bool IsContraindicated,
    IReadOnlyList<string> ActiveInjurySites,
    string? Reason);

/// <summary>A request to replace one exercise in a session.</summary>
public sealed record SwapExerciseRequest(
    Guid WorkoutDayExerciseId,
    int NewExerciseDefinitionId,
    bool SavePreference);

/// <summary>The exercise that replaced it, with the reason the original was refused.</summary>
public sealed record SwapExerciseResponse(
    bool Success,
    string Message);
