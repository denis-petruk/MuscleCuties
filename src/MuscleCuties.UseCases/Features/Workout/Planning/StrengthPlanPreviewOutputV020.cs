namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The movement slot a generated exercise fills, which the client uses to group the session.</summary>
public enum StrengthMovementSlotDto
{
    KneeDominant = 0,
    HipDominant = 1,
    UpperPush = 2,
    UpperPull = 3,
    Trunk = 4,
    GoalSpecificAccessory = 5
}

/// <summary>Transport representation of a selected workout slot, without Core domain types.</summary>
public sealed record StrengthPlanSlotOutputV020(
    string SlotId,
    int MovementSlot,
    bool IsRequired,
    int PrescriptionRole,
    bool IsHeavyMain,
    int? ExerciseId,
    string? ExerciseName,
    string? SelectionReason,
    string? AppliedPrescriptionRule,
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds,
    IReadOnlyList<string> LimitationCodes)
{
    /// <summary>Repetitions the movement starts at.</summary>
    public int? InitialRepetitionTarget => MinimumRepetitions;
}

/// <summary>One day of a generated preview, before it is accepted as a plan.</summary>
public sealed record StrengthPlanPreviewSessionV020(
    string TemplateId,
    string TemplateVersion,
    int SessionIndex,
    decimal? EstimatedMinutes,
    IReadOnlyList<StrengthPlanSlotOutputV020> Slots,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyDictionary<string, string> SlotReviewConditions,
    DayOfWeek? PlannedWeekday = null);

/// <summary><c>GeneralConditions</c> keeps its wire name and now carries only real review
/// conditions; it is null when the review supplied none.</summary>
public sealed record StrengthExerciseReviewConditionV020(
    int ExerciseId,
    string ExerciseName,
    string? ExperienceConditions,
    string? GeneralConditions);

/// <summary>A complete preview plus the input fingerprints it was generated from.</summary>
public sealed record StrengthPlanPreviewResponseV020(
    string PolicyId,
    string PolicyVersion,
    string RulesetVersion,
    string Status,
    int Experience,
    IReadOnlyList<StrengthPlanPreviewSessionV020> Sessions,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<StrengthExerciseReviewConditionV020> ExerciseReviewConditions,
    IReadOnlyList<string> AdvisoryCodes,
    MixedActivityScheduleV020? MixedActivitySchedule = null,
    int? EffectiveActivityTypeId = null,
    string? FallbackSource = null,
    string? FallbackReasonCode = null,
    bool RequiresReducedVolumeAcknowledgment = false)
{
    /// <summary>The shortest session the inputs allow, which the plan may not undercut.</summary>
    public decimal? MinimumRequiredSessionMinutes { get; init; }
    /// <summary>The profile version the preview read, so a newer profile makes it stale.</summary>
    public int? SourceProfileVersion { get; init; }
    /// <summary>The injury state it was built from, compared on accept.</summary>
    public string? SourceInjuryFingerprint { get; init; }
    /// <summary>The plan that was in force, so acceptance knows what it replaces.</summary>
    public Guid? SourceActivePlanId { get; init; }
    /// <summary>That plan's revision, which acceptance must still find current.</summary>
    public Guid? SourceActiveRevisionId { get; init; }
    /// <summary>The training history it read, compared on accept.</summary>
    public string? SourceHistoryFingerprint { get; init; }
    /// <summary>The catalog version it read, so a catalog change invalidates it.</summary>
    public string? SourceCatalogFingerprint { get; init; }
    /// <summary>The tz version its local dates were computed against.</summary>
    public long SourceTimeZoneVersion { get; init; }
    /// <summary>The experience version it assumed.</summary>
    public long SourceExperienceVersion { get; init; }
    /// <summary>The activity-answer version it assumed.</summary>
    public long SourceAnswerVersion { get; init; }
    /// <summary>The observed-history fingerprint it read.</summary>
    public string? SourceObservedHistoryFingerprint { get; init; }
    /// <summary>The readiness revision it read, null when readiness played no part.</summary>
    public long? SourceReadinessRevision { get; init; }
    /// <summary>The muscle groupings it assumed, so a catalog change is detectable.</summary>
    public IReadOnlyDictionary<int, string[]>? SourceMuscleGroupsByExerciseId { get; init; }
    /// <summary>The styles that fit these inputs; more than one means she must choose.</summary>
    public IReadOnlyList<int> FittingStrengthStyleIds { get; init; } = [];
    /// <summary>Whether the preview cannot be accepted until she picks a style.</summary>
    public bool StrengthStyleChoiceRequired { get; init; }
    /// <summary>The injury provenance it recorded, so the omitted slots can be attributed later.</summary>
    public string? InjuryProvenanceFingerprint { get; init; }

    /// <summary>Workout Engine V2 result computed for this same request. Absent when the preview is
    /// refused before planning, so a consumer never reads a V2 plan the preview did not approve.</summary>
    public GenerationPlanV2? GenerationV2 { get; init; }
}

/// <summary>Read-only projection of <c>WorkoutGenerationResultV2</c> for preview consumers.</summary>
public sealed record GenerationPlanV2(
    string Status,
    string PolicyVersion,
    string InputContractVersion,
    string CatalogVersion,
    int SessionCount,
    string PrescriptionHash,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<string> AdvisoryCodes,
    IReadOnlyList<string> RuleTrace)
{
    /// <summary>The generated sessions in order.</summary>
    public IReadOnlyList<GenerationSessionV2> Sessions { get; init; } = [];
    /// <summary>What the inputs could not satisfy, surfaced to her rather than silently relaxed.</summary>
    public IReadOnlyList<string> MissingGaps { get; init; } = [];
    /// <summary>Whether today's readiness changed the generated doses.</summary>
    public bool ReadinessAdaptationApplied { get; init; }
    /// <summary>The explanation shown beside the plan, in the planner's own words.</summary>
    public string? CoachExplanation { get; init; }
}

/// <summary>One exercise the version-2 planner generated for a session.</summary>
public sealed record GenerationExerciseV2(
    int OrderIndex, int ExerciseId, string ExerciseName, int WarmUpSets, int WorkingSets,
    int MinRepetitions, int MaxRepetitions, int TargetRir, int RestSeconds,
    string LoadStatus, decimal? PrescribedLoadKg, int? SupersetGroupId,
    bool IsUnilateral, string SelectionReasonCode)
{
    /// <summary>Whether the exercise may be skipped without the session counting as incomplete.</summary>
    public bool IsOptional { get; init; }
    /// <summary>The movement pattern it fills, which is what the slot rules match on.</summary>
    public string Pattern { get; init; } = string.Empty;
    /// <summary>The slot it was chosen for, so a restore can put it back in the same place.</summary>
    public string SlotId { get; init; } = string.Empty;
}

/// <summary>The finisher the planner attached to a session, with its own prescription.</summary>
public sealed record GenerationFinisherV2(
    int ActivityTypeId, int DurationMinutes, string IntensityBand, int TargetRpeMin, int TargetRpeMax);

/// <summary>A generated session as the planner and the stored snapshot both read it.</summary>
public sealed record GenerationSessionV2(
    int SessionIndex, DayOfWeek Weekday, DateOnly LocalDate, int ActivityTypeId,
    int GeneralWarmupMinutes, decimal EstimatedTotalMinutes,
    IReadOnlyList<GenerationExerciseV2> Exercises, GenerationFinisherV2? AttachedFinisher = null);

/// <summary>One activity session inside a mixed week, with its occurrence rule.</summary>
public sealed record MixedActivityScheduleSessionV020(
    int ActivityTypeId,
    DayOfWeek Weekday,
    decimal DurationMinutes,
    bool IsHardSession,
    int? DayOrder = null,
    ActivitySessionPrescriptionOutputV020? OccurrencePrescription = null);

/// <summary>The prescription of one activity session, as generated.</summary>
public sealed record ActivitySessionPrescriptionOutputV020(
    int ActivityTypeId, string Purpose, string Modality, string Intensity, string TrainingStyle,
    int FrequencyPerWeek, decimal? WarmupMinutes, decimal? MainMinutes, decimal? CooldownMinutes,
    int? Sets, int? MinimumRepetitions, int? MaximumRepetitions, int? RepetitionsInReserve,
    int? RestSeconds, int? IntervalWorkCount, decimal? IntervalWorkSeconds,
    int? IntervalRecoveryCount, decimal? IntervalRecoverySeconds, bool RequiresUserConfirmation,
    IReadOnlyList<string> Reasons);

/// <summary>The reason the mixed schedule chose the activities it did.</summary>
public sealed record MixedActivityScheduleDecisionV020(
    int ActivityTypeId,
    string Disposition,
    string? PrescriptionStatus,
    int? PolicySessionsPerWeek,
    int? TargetSessionsPerWeek,
    int ScheduledSessionsPerWeek,
    IReadOnlyList<string> ReasonCodes,
    string? Modality = null,
    int? EffectiveActivityTypeId = null,
    string? FallbackReasonCode = null,
    int? MaxSessionsPerWeek = null,
    ActivitySessionPrescriptionOutputV020? OriginalPrescription = null);

/// <summary>The week's activity sessions with the decision that produced them.</summary>
public sealed record MixedActivityScheduleV020(
    IReadOnlyList<MixedActivityScheduleSessionV020> Sessions,
    IReadOnlyList<MixedActivityScheduleDecisionV020> Activities,
    IReadOnlyList<string> InputReasonCodes);
