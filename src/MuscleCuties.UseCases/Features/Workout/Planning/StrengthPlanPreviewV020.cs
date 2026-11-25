using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The client's preview request: its draft choices layered over the stored profile.</summary>
public sealed record StrengthPlanPreviewRequestV020(
    WorkoutActivityDto Activity,
    DateOnly LocalDate,
    ActivityTrainingHistoryInputV020? ActivityHistory,
    HashSet<int>? ExcludedExerciseIds = null,
    HashSet<int>? UserConfirmedFamiliarExerciseIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    HashSet<int>? UserConfirmedManageableLoadExerciseIds = null,
    HashSet<string>? AcknowledgedSubstitutionPairKeys = null,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null)
{
    /// <summary>Unsaved preference edits, applied for this preview only.</summary>
    public WorkoutPreferencesDto? DraftPreferences { get; init; }
    /// <summary>Whether a shorter session may drop movements instead of shortening sets.</summary>
    public bool AllowMovementOmissionsForDuration { get; init; }
    /// <summary>The style she picked, which overrides the inferred one.</summary>
    public WorkoutActivityDto? ExplicitStrengthStyleChoice { get; init; }
    /// <summary>Saved user activity choices used to build the mixed weekly schedule.</summary>
    public HashSet<int>? SelectedActivityTypes { get; init; }

    /// <summary>The weekdays she can train in this draft, overriding the stored set.</summary>
    public HashSet<DayOfWeek>? AvailableWeekdays { get; init; }

    /// <summary>Saved user session durations; explicit activity answers may refine these for this preview.</summary>
    public IReadOnlyDictionary<int, int>? ActivitySessionDurationsMinutes { get; init; }

    /// <summary>Saved automatic weekly allocations for activities with a user-configurable frequency.</summary>
    public IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek { get; init; }

    /// <summary>Explicit per-activity facts needed by the mixed planner; absent values remain unknown.</summary>
    public IReadOnlyList<ActivityEligibilityInputV020>? ActivityEligibilityEvidence { get; init; }

    /// <summary>The cardio activity HIIT is built on, when she chose HIIT.</summary>
    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }

    /// <summary>The evidence backing HIIT eligibility for this draft.</summary>
    public HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence { get; init; }

    /// <summary>Input contract version the caller built this request against. Null keeps older
    /// clients working; any other value must match the build or the preview is refused.</summary>
    public string? InputContractVersion { get; init; }
}

/// <summary>One activity's eligibility answers, which is what allows it to be scheduled.</summary>
public sealed record ActivityEligibilityInputV020(
    WorkoutActivityDto Activity,
    ActivityTrainingHistoryInputV020? ActivityHistory = null,
    bool? HasSwimmingFacilityAccess = null,
    bool? CanSwimIndependently = null,
    bool? WantsContinuousSwimming = null,
    bool? HasRopeClimbingCompetence = null,
    bool? AppropriateClimbingInstructionAvailable = null,
    bool? IsDemandingYogaContent = null,
    int? YogaSessionDurationMinutes = null);

/// <summary>The HIIT evidence she has provided, without which HIIT is not offered.</summary>
public sealed record HiitEligibilityEvidenceInputV020(
    ActivityTrainingHistoryInputV020? UnderlyingCardioHistory = null,
    IReadOnlyList<HiitAerobicWeekInputV020>? PreviousFourWeeksAerobicEvidence = null,
    bool? ExistingClearanceScreeningSatisfied = null);

/// <summary>A week of aerobic evidence, which the HIIT eligibility rule reads.</summary>
public sealed record HiitAerobicWeekInputV020(
    int WeekOffsetFromCurrent,
    int QualifyingManageableAerobicSessionCount);

/// <summary>What she has done of an activity, used to judge whether it is still new.</summary>
public sealed record ActivityTrainingHistoryInputV020(
    string ActivityId,
    int? ConsistentParticipationWeeks,
    decimal? AverageSessionsPerWeekInRecentFourWeeks,
    DateOnly? LastCompletedSessionDate,
    bool IsSelfReported);

/// <summary>Everything a preview is generated from, read once so the plan cannot mix versions.</summary>
public sealed record StrengthPlanPreviewSourceV020(
    UserGoal Goal,
    Equipment Equipment,
    InjuryFlag ActiveInjuryFlags,
    bool HasCurrentSymptoms,
    IReadOnlyList<StrengthExerciseCatalogEntry> Catalog,
    IReadOnlyDictionary<int, int> PreferredExerciseByOriginalId,
    IReadOnlySet<int> AcceptedExerciseIds,
    IReadOnlyDictionary<int, int> CompletedExerciseExposureCounts,
    IReadOnlySet<int> KnownManageableLoadExerciseIds,
    IReadOnlyDictionary<int, int> SecondsPerRepByExerciseId,
    IReadOnlyDictionary<int, string> PrimaryMuscleByExerciseId,
    IReadOnlyDictionary<string, IReadOnlySet<int>> ReviewedAllowedExerciseIdsByTemplateSlot,
    IReadOnlyDictionary<string, string> TemplateSlotReviewConditions)
{
    /// <summary>Used for age in the safety and calorie rules.</summary>
    public DateOnly? BirthDate { get; init; }
    /// <summary>Restricts which movements and intensities are allowed.</summary>
    public PregnancyStatus PregnancyStatus { get; init; } = PregnancyStatus.No;
    /// <summary>The zone every local date in the plan is computed in.</summary>
    public string AccountTimeZoneId { get; init; } = "UTC";
    /// <summary>How many distinct days she has trained, which drives progression.</summary>
    public int DistinctCompletedWorkoutDays { get; init; }
    /// <summary>Whether she is coming back from a break, which caps the starting load.</summary>
    public bool IsReturningAfterDetraining { get; init; }
    /// <summary>The sets the device observed, the only evidence progression may use.</summary>
    public IReadOnlyList<ObservedSetRecordV2> PriorObservedSets { get; init; } = [];
    /// <summary>What she actually did for cardio, which the activity rules read.</summary>
    public IReadOnlyList<CardioActualRecordV2> PriorCardioActuals { get; init; } = [];
    /// <summary>Pain that is still active, which withholds the movements it conflicts with.</summary>
    public IReadOnlyList<PainRestrictionV2> PainRestrictions { get; init; } = [];
    /// <summary>Today's readiness, when it has been recorded.</summary>
    public DailyReadinessInputV2? DailyReadiness { get; init; }
    /// <summary>The activity types she wants, as catalog ids.</summary>
    public IReadOnlySet<int>? SelectedActivityTypes { get; init; }
    /// <summary>Exercises she excluded, which are never prescribed.</summary>
    public IReadOnlySet<int>? ExcludedExerciseIds { get; init; }
    /// <summary>The weekdays she can train.</summary>
    public IReadOnlySet<DayOfWeek>? AvailableWeekdays { get; init; }
    /// <summary>Minutes available per activity type.</summary>
    public IReadOnlyDictionary<int, int>? ActivitySessionDurationsMinutes { get; init; }
    /// <summary>Wanted sessions per activity type.</summary>
    public IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek { get; init; }
    /// <summary>The level the planner may prescribe for.</summary>
    public TrainingExperienceLevel? ProfileTrainingExperience { get; init; }
    /// <summary>How strongly each exercise serves her goal, used to pick within a slot.</summary>
    public IReadOnlyDictionary<int, int>? GoalPriorityByExerciseId { get; init; }
    /// <summary>The muscle groupings from the catalog, so coverage can be judged.</summary>
    public IReadOnlyDictionary<int, IReadOnlySet<string>>? MuscleGroupsByExerciseId { get; init; }
    /// <summary>The reviewed volume floors, which a shortened session may not fall below.</summary>
    public IReadOnlyDictionary<string, ReviewedTemplateFloorDto>? TemplateVolumeFloors { get; init; }
    /// <summary>The profile version read, recorded so a stale preview is detectable.</summary>
    public int? ProfileVersion { get; init; }
    /// <summary>Server-derived scope limitation; absent in pure generator fixtures.</summary>
    public string? PopulationLimitationCode { get; init; }
    /// <summary>The injury state read.</summary>
    public string? InjuryFingerprint { get; init; }
    /// <summary>The plan in force, which acceptance replaces.</summary>
    public Guid? ActivePlanId { get; init; }
    /// <summary>That plan's revision.</summary>
    public Guid? ActiveRevisionId { get; init; }
    /// <summary>The training-history state read.</summary>
    public string? HistoryFingerprint { get; init; }
    /// <summary>The catalog version read.</summary>
    public string? CatalogFingerprint { get; init; }
    /// <summary>The tz version the local dates were resolved against.</summary>
    public long TimeZoneVersion { get; init; }
    /// <summary>Today in her zone, which dates the generated week.</summary>
    public DateOnly? CurrentAccountLocalDate { get; init; }
    /// <summary>The experience version assumed.</summary>
    public long ExperienceVersion { get; init; }
    /// <summary>The activity-answer version assumed.</summary>
    public long AnswerVersion { get; init; }
    /// <summary>The observed-history state read.</summary>
    public string? ObservedHistoryFingerprint { get; init; }
    /// <summary>The readiness revision read, null when readiness played no part.</summary>
    public long? ReadinessRevision { get; init; }
    /// <summary>The stored activity answers, folded into the schedule.</summary>
    public SavedActivityAnswersDto? SavedAnswers { get; init; }
    /// <summary>Activities an active injury blocks under the reviewed site/status rules.
    /// Strength styles are never listed; their safety is enforced per exercise.</summary>
    public IReadOnlySet<int>? RestrictedActivityTypes { get; init; }

    /// <summary>Movement patterns blocked by recorded injury safety answers. Null means no
    /// answer-driven evidence, not that no injury exists.</summary>
    public IReadOnlySet<MovementPattern>? BlockedMovementPatterns { get; init; }

    /// <summary>True when the saved equipment answer is missing or unreadable. <see cref="Equipment"/>
    /// then holds a placeholder that planning never uses; a draft equipment level can supply it.</summary>
    public bool EquipmentAnswerMissing { get; init; }

    /// <summary>True when the saved goal answer is missing or unreadable; <see cref="Goal"/> is a placeholder.</summary>
    public bool GoalAnswerMissing { get; init; }

    /// <summary>User-selected glute focus, stored on the profile. Absent means no focus.</summary>
    public bool GluteFocus { get; init; }

    /// <summary>How each exercise's work is shared across muscles.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<CatalogMuscleRow>>? MuscleContributionsByExerciseId { get; init; }
    /// <summary>The equipment available in this draft, overriding the stored level.</summary>
    public EquipmentCapabilities? DraftEquipmentCapabilities { get; init; }
}

/// <summary>A reviewed minimum volume for a template, which the planner may not go below.</summary>
public sealed record ReviewedTemplateFloorDto(string TemplateId, int MinimumWorkingSets, int MinimumExercises,
    IReadOnlyDictionary<string, int> MinimumSetsBySlotId);

/// <summary>Loads the preview inputs, so the planner never touches storage itself.</summary>
public interface IStrengthPlanPreviewSourceV020
{
    Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}
