namespace MuscleCuties.UseCases.Features.Workout.Session.Contracts;

/// <summary>A set as the device observed it, which is the evidence progression is allowed to use.</summary>
public sealed record ObservedSetRecord(
    int SetIndex,
    int RepsCompleted,
    decimal LoadKg,
    int? RirActual = null,
    bool TechniqueAcceptable = true,
    int? LeftSideReps = null,
    int? RightSideReps = null);

/// <summary>One exercise's logged work in a session, as the archive shows it.</summary>
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
    int? EffortRating = null,
    IReadOnlyList<ObservedSetRecord>? ObservedSets = null);

/// <summary>What was actually done on a non-strength day, which has no sets to record.</summary>
public sealed record ActivityActualLogDto(
    int? WorkoutDayActivityId,
    MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto ActivityType,
    int DurationSeconds,
    decimal? DistanceKm = null,
    int? DistanceMeters = null,
    int? PaceSecondsPerKm = null,
    int? PaceSecondsPer100m = null,
    int? AverageHeartRateBpm = null,
    int? PowerWatts = null,
    int? Cadence = null,
    int? StrokeCount = null,
    int? EffortRpe = null,
    string? Notes = null);

/// <summary>A completed session sent up in one call, idempotent on its key.</summary>
public sealed record LogWorkoutSessionRequest(
    int WorkoutDayId,
    DateOnly Date,
    IReadOnlyList<WorkoutExerciseLogDto>? Exercises = null,
    Guid? PlannedSessionId = null,
    IReadOnlyList<ActivityActualLogDto>? ActivityActuals = null);

/// <summary>A request to replace one prescribed exercise with an eligible alternative.</summary>
public sealed record SwapWorkoutExerciseRequest(
    int WorkoutDayExerciseId,
    int NewExerciseId,
    bool SavePreference);

/// <summary>An alternative the client may offer: eligible by pattern, equipment and restrictions.</summary>
public sealed record WorkoutSwapCandidateDto(
    int ExerciseId,
    string Name,
    string MuscleMatch,
    string Equipment,
    string PrimaryMuscles);

/// <summary>Her note on a movement, saved against the prescribed row.</summary>
public sealed record SaveWorkoutMovementNoteRequest(int WorkoutDayExerciseId, string? Notes);
/// <summary>Marks a prescribed exercise optional for this session only.</summary>
public sealed record SetWorkoutExerciseOptionalRequest(int WorkoutDayExerciseId, bool IsOptional, int OptionalSets);
/// <summary>Splits one exercise out of a superset for the rest of the session.</summary>
public sealed record SplitWorkoutSupersetRequest(Guid SessionId, int WorkoutDayExerciseId, int SupersetGroupId);

/// <summary>Her saved note on a movement, returned so the client can echo it.</summary>
public sealed record WorkoutMovementNoteDto(int WorkoutDayExerciseId, string Notes);

/// <summary>One set of the session as the client renders it: what was done and how it was judged.</summary>
public sealed record WorkoutSetDetailDto(
    int SetNumber,
    double WeightKg,
    int RepsCompleted,
    double? Rpe,
    bool IsWarmup,
    int RestTimeSeconds,
    int? LeftSideReps = null,
    int? RightSideReps = null)
{
    /// <summary>Whether the device saw the set rather than it being typed in afterwards.</summary>
    public bool IsObserved { get; init; }
    /// <summary>Whether the technique was judged acceptable; a false value blocks a load increase.</summary>
    public bool TechniqueAcceptable { get; init; } = true;
}

/// <summary>One prescribed exercise with its targets, its display strings and what has been logged so far.</summary>
public sealed record WorkoutExerciseDetailDto(
    int WorkoutDayExerciseId,
    int ExerciseId,
    string Name,
    string ActivityTag,
    bool IsLogged,
    IReadOnlyList<WorkoutSetDetailDto> Sets)
{
    /// <summary>Working sets prescribed, excluding warm-ups.</summary>
    public int PlannedSets { get; init; }
    /// <summary>Warm-up sets prescribed, which never count as volume.</summary>
    public int PlannedWarmUpSets { get; init; }
    /// <summary>Bottom of the rep range, where load is reduced.</summary>
    public int MinRepetitions { get; init; }
    /// <summary>Top of the rep range, where load increases.</summary>
    public int MaxRepetitions { get; init; }
    /// <summary>Effort target in reps in reserve, which is how the set is judged.</summary>
    public int TargetRir { get; init; }
    /// <summary>Rest prescribed between sets, in seconds.</summary>
    public int RestSeconds { get; init; }
    /// <summary>The superset it belongs to; exercises sharing it are performed back to back.</summary>
    public int? SupersetGroupId { get; init; }
    /// <summary>Whether the work is per side, which doubles its time.</summary>
    public bool IsUnilateral { get; init; }
    /// <summary>Whether the load was prescribed, held or unknown — the reason a weight may be absent.</summary>
    public string LoadStatus { get; init; } = string.Empty;
    /// <summary>The prescribed load in kilograms; null when none was prescribed.</summary>
    public decimal? PrescribedLoadKg { get; init; }
    /// <summary>Whether she must calibrate this movement before a load can be prescribed.</summary>
    public bool RequiresInitialCalibration { get; init; }
    /// <summary>Sets she may skip without the session counting as incomplete.</summary>
    public int OptionalSets { get; init; }
    /// <summary>Whether the whole exercise is optional for this session.</summary>
    public bool IsOptional { get; init; }
    /// <summary>The exercise's display title, already resolved server-side.</summary>
    public string ActivityTitle { get; init; } = string.Empty;
    /// <summary>How to perform it, already written for display.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Illustration URL, resolved to whichever artwork this account gets.</summary>
    public string ImageUrl { get; init; } = string.Empty;
    /// <summary>Demonstration video URL, empty when the exercise has none.</summary>
    public string VideoUrl { get; init; } = string.Empty;
    /// <summary>Coaching notes shown while the set is performed.</summary>
    public string TechniqueNotes { get; init; } = string.Empty;
    /// <summary>Pre-formatted tips line, so the client does not compose coaching text.</summary>
    public string QuickTipsText { get; init; } = string.Empty;
    /// <summary>The target rendered as the client shows it, so reps and effort read the same on every platform.</summary>
    public string TargetText { get; init; } = string.Empty;
    /// <summary>What she did last time, pre-formatted for the same reason.</summary>
    public string PreviousText { get; init; } = string.Empty;
    /// <summary>The progression recommendation in words, produced where the rules live.</summary>
    public string RecommendationText { get; init; } = string.Empty;
    /// <summary>The recommendation's stable code, which the client may style differently.</summary>
    public string RecommendationCode { get; init; } = string.Empty;
    /// <summary>The load the recommendation proposes, in kilograms.</summary>
    public decimal? RecommendationLoadKg { get; init; }
    /// <summary>Her own note on this movement.</summary>
    public string MovementNotes { get; init; } = string.Empty;
    /// <summary>Sets already logged in this session, pre-formatted.</summary>
    public string LoggedSetsText { get; init; } = string.Empty;
    /// <summary>Reps already logged, pre-formatted.</summary>
    public string LoggedRepsText { get; init; } = string.Empty;
    /// <summary>Weight already logged, pre-formatted in her unit.</summary>
    public string LoggedWeightText { get; init; } = string.Empty;
    /// <summary>Duration already logged, pre-formatted in minutes.</summary>
    public string LoggedDurationMinutesText { get; init; } = string.Empty;
    /// <summary>Distance already logged, pre-formatted in her unit.</summary>
    public string LoggedDistanceKmText { get; init; } = string.Empty;
    /// <summary>Pace already logged, pre-formatted.</summary>
    public string LoggedPaceText { get; init; } = string.Empty;
    /// <summary>Heart rate already logged, pre-formatted.</summary>
    public string LoggedHeartRateText { get; init; } = string.Empty;
    /// <summary>Power already logged, pre-formatted.</summary>
    public string LoggedPowerWattsText { get; init; } = string.Empty;
    /// <summary>Cadence already logged, pre-formatted.</summary>
    public string LoggedCadenceRpmText { get; init; } = string.Empty;
    /// <summary>Effort already logged, pre-formatted.</summary>
    public string LoggedEffortText { get; init; } = string.Empty;
    /// <summary>The duration column's label, so the client does not translate a unit per platform.</summary>
    public string DurationLabel { get; init; } = string.Empty;
    /// <summary>The distance column's label.</summary>
    public string DistanceLabel { get; init; } = string.Empty;
    /// <summary>The pace column's label.</summary>
    public string PaceLabel { get; init; } = string.Empty;
    /// <summary>Whether any endurance metric applies to this exercise.</summary>
    public bool UsesEnduranceMetrics { get; init; }
    /// <summary>Whether a duration field applies, which decides if the field is shown at all.</summary>
    public bool UsesDurationMetric { get; init; }
    /// <summary>Whether a distance field applies.</summary>
    public bool UsesDistanceMetric { get; init; }
    /// <summary>Whether a pace field applies.</summary>
    public bool UsesPaceMetric { get; init; }
    /// <summary>Whether a heart-rate field applies.</summary>
    public bool UsesHeartRateMetric { get; init; }
    /// <summary>Whether a power field applies.</summary>
    public bool UsesPowerMetric { get; init; }
    /// <summary>Whether a cadence field applies.</summary>
    public bool UsesCadenceMetric { get; init; }
    /// <summary>Whether an effort field applies.</summary>
    public bool UsesEffortMetric { get; init; }
    /// <summary>Whether a weight field applies; false for bodyweight and endurance work.</summary>
    public bool UsesWeight { get; init; }
}

/// <summary>A non-strength day's prescription as the client renders it.</summary>
public sealed record WorkoutActivityDetailDto(
    int OrderIndex,
    int TotalActivities,
    string Tag,
    string Title,
    string Subtitle,
    string MetricText,
    string SummaryText,
    IReadOnlyList<int> WorkoutDayExerciseIds)
{
    /// <summary>The prescription this day carries, absent for a day planned by hand.</summary>
    public MuscleCuties.UseCases.Features.Workout.Planning.ActivitySessionPrescriptionOutputV020? Prescription { get; init; }
    /// <summary>The activity this day prescribes, resolved from the catalog.</summary>
    public MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto ActivityType { get; init; }
        = MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto.Other;
}

/// <summary>Why today's dose was adjusted, ordered by how firmly the evidence supports it.</summary>
public enum WorkoutDoseAdjustmentReason { DailyReadiness, ProvisionalPhaseFeeling, RepeatedPhasePattern, SafetyReview }
/// <summary>How strong that evidence was, which the client shows next to the reason.</summary>
public enum WorkoutDoseAdjustmentConfidence { Provisional, Repeated, Current }

/// <summary>The session the client opens: the warm-up, the exercises and any dose adjustment applied.</summary>
public sealed record WorkoutSessionDetailDto(
    int WorkoutDayId,
    string Title,
    string Subtitle,
    string SummaryText,
    bool IsRestDay,
    IReadOnlyList<WorkoutExerciseDetailDto> Exercises,
    IReadOnlyList<WorkoutActivityDetailDto> Activities)
{
    /// <summary>The general warm-up minutes, null when the day has none.</summary>
    public int? GeneralWarmupMinutes { get; init; }
    /// <summary>Estimated session length, which the time budget was fitted to.</summary>
    public decimal? EstimatedTotalMinutes { get; init; }
    /// <summary>The finisher attached to the session, when the planner added one.</summary>
    public MuscleCuties.UseCases.Features.Workout.Planning.GenerationFinisherV2? AttachedFinisher { get; init; }
    /// <summary>Whether the day itself is optional.</summary>
    public bool IsOptional { get; init; }
    /// <summary>Whether this is a deliberately easier variant rather than the generated day.</summary>
    public bool IsEasierAlternative { get; init; }
    /// <summary>Why today's dose differs from the plan, null when it does not.</summary>
    public WorkoutDoseAdjustmentReason? DoseAdjustmentReason { get; init; }
    /// <summary>How strong the evidence behind that adjustment was.</summary>
    public WorkoutDoseAdjustmentConfidence? DoseAdjustmentConfidence { get; init; }
    /// <summary>The policy version that produced the adjustment, so a support report can date it.</summary>
    public string? DoseAdjustmentPolicyVersion { get; init; }
}
