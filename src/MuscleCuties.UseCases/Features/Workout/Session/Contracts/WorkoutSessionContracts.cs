namespace MuscleCuties.UseCases.Features.Workout.Session.Contracts;

public sealed record WorkoutExerciseLogDto(
    int WorkoutDayExerciseId,
    int ExerciseId,
    int CompletedSets,
    int CompletedReps,
    float? WeightKg,
    int? CompletedDurationSeconds = null,
    float? DistanceKm = null,
    int? AverageHeartRateBpm = null,
    int? PaceSecondsPerKm = null,
    int? PowerWatts = null,
    int? CadenceRpm = null,
    int? EffortRating = null);

public sealed record LogWorkoutSessionRequest(
    int WorkoutDayId,
    DateOnly Date,
    IReadOnlyList<WorkoutExerciseLogDto> Exercises,
    Guid? PlannedSessionId = null);

public sealed record SwapWorkoutExerciseRequest(
    int WorkoutDayExerciseId,
    int NewExerciseId,
    bool SavePreference);

public sealed record WorkoutSwapCandidateDto(
    int ExerciseId,
    string Name,
    string MuscleMatch,
    string Equipment,
    string PrimaryMuscles);

public sealed record SaveWorkoutMovementNoteRequest(int WorkoutDayExerciseId, string? Notes);
public sealed record SetWorkoutExerciseOptionalRequest(int WorkoutDayExerciseId, bool IsOptional, int OptionalSets);

public sealed record WorkoutMovementNoteDto(int WorkoutDayExerciseId, string Notes);

public sealed record WorkoutSetDetailDto(
    int SetNumber,
    double WeightKg,
    int RepsCompleted,
    double? Rpe,
    bool IsWarmup,
    int RestTimeSeconds);

public sealed record WorkoutExerciseDetailDto(
    int WorkoutDayExerciseId,
    int ExerciseId,
    string Name,
    string ActivityTag,
    bool IsLogged,
    IReadOnlyList<WorkoutSetDetailDto> Sets)
{
    public int PlannedSets { get; init; }
    public int OptionalSets { get; init; }
    public bool IsOptional { get; init; }
    public string ActivityTitle { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public string TechniqueNotes { get; init; } = string.Empty;
    public string QuickTipsText { get; init; } = string.Empty;
    public string TargetText { get; init; } = string.Empty;
    public string PreviousText { get; init; } = string.Empty;
    public string RecommendationText { get; init; } = string.Empty;
    public string MovementNotes { get; init; } = string.Empty;
    public string LoggedSetsText { get; init; } = string.Empty;
    public string LoggedRepsText { get; init; } = string.Empty;
    public string LoggedWeightText { get; init; } = string.Empty;
    public string LoggedDurationMinutesText { get; init; } = string.Empty;
    public string LoggedDistanceKmText { get; init; } = string.Empty;
    public string LoggedPaceText { get; init; } = string.Empty;
    public string LoggedHeartRateText { get; init; } = string.Empty;
    public string LoggedPowerWattsText { get; init; } = string.Empty;
    public string LoggedCadenceRpmText { get; init; } = string.Empty;
    public string LoggedEffortText { get; init; } = string.Empty;
    public string DurationLabel { get; init; } = string.Empty;
    public string DistanceLabel { get; init; } = string.Empty;
    public string PaceLabel { get; init; } = string.Empty;
    public bool UsesEnduranceMetrics { get; init; }
    public bool UsesDurationMetric { get; init; }
    public bool UsesDistanceMetric { get; init; }
    public bool UsesPaceMetric { get; init; }
    public bool UsesHeartRateMetric { get; init; }
    public bool UsesPowerMetric { get; init; }
    public bool UsesCadenceMetric { get; init; }
    public bool UsesEffortMetric { get; init; }
    public bool UsesWeight { get; init; }
}

public sealed record WorkoutActivityDetailDto(
    int OrderIndex,
    int TotalActivities,
    string Tag,
    string Title,
    string Subtitle,
    string MetricText,
    string SummaryText,
    IReadOnlyList<int> WorkoutDayExerciseIds);

public sealed record WorkoutSessionDetailDto(
    int WorkoutDayId,
    string Title,
    string Subtitle,
    string SummaryText,
    bool IsRestDay,
    IReadOnlyList<WorkoutExerciseDetailDto> Exercises,
    IReadOnlyList<WorkoutActivityDetailDto> Activities)
{
    public bool IsOptional { get; init; }
    public bool IsEasierAlternative { get; init; }
}
