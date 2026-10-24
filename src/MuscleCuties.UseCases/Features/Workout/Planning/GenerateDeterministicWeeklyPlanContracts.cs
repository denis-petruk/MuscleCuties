using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record DeterministicWeeklyPlanRequestDto(
    string Goal,
    string? GoalPace,
    string TrainingExperience,
    IReadOnlyList<WorkoutActivityDto> PreferredActivities,
    int GeneralDaysPerWeek,
    int PreferredSessionLengthMinutes,
    string AvailableEquipment,
    string StrengthStyle,
    IReadOnlyList<DayOfWeek> AvailableWeekdays,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null,
    IReadOnlyList<int>? ExcludedExerciseIds = null,
    bool ApplyChanges = false,
    DateOnly PlanWeekStartDate = default);

public sealed record PlannedExerciseDto(
    int ExerciseId,
    string ExerciseName,
    string PrimaryMuscle,
    bool IsCompound,
    bool IsAccessory,
    bool IsOptional,
    int TargetSets,
    int MinReps,
    int MaxReps,
    int TargetRir,
    decimal PrescribedLoadKg,
    string ProgressionAction,
    string ProgressionRationale);

public sealed record PlannedSessionDto(
    Guid SessionId,
    WorkoutActivityDto Activity,
    DayOfWeek DayOfWeek,
    int EstimatedMinutes,
    bool IsHardSession,
    IReadOnlyList<PlannedExerciseDto> Exercises,
    string? Notes = null);

public sealed record RuleTraceEntryDto(
    int StepNumber,
    string StepName,
    string DecisionSummary,
    IReadOnlyDictionary<string, string> Details);

public sealed record DeterministicWeeklyPlanResponseDto(
    Guid PlanId,
    DateOnly PlanWeekStartDate,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<PlannedSessionDto> Sessions,
    IReadOnlyDictionary<string, int> HypertrophySetsByMuscle,
    bool HypertrophyAllMusclesSatisfied,
    decimal WeeklyModerateEquivalentMinutes,
    bool MeetsWhoHealthTargets,
    bool IsDeloadWeek,
    string DeloadReason,
    string EffectivePrecedenceTier,
    bool IsPrecedenceApplied,
    bool HasSafetyBlock,
    IReadOnlyList<RuleTraceEntryDto> RuleTrace);
