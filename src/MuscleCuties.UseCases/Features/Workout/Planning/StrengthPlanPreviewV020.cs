using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthPlanPreviewRequestV020(
    WorkoutActivityDto Activity,
    DateOnly LocalDate,
    ActivityTrainingHistoryInputV020? ActivityHistory,
    HashSet<int>? ExcludedExerciseIds = null,
    HashSet<int>? UserConfirmedFamiliarExerciseIds = null,
    HashSet<int>? AcknowledgedExerciseConditionIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    HashSet<int>? UserConfirmedManageableLoadExerciseIds = null,
    HashSet<string>? AcknowledgedSubstitutionPairKeys = null,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null)
{
    public WorkoutPreferencesDto? DraftPreferences { get; init; }
    public WorkoutActivityDto? ExplicitStrengthStyleChoice { get; init; }
    /// <summary>Saved user activity choices used to build the mixed weekly schedule.</summary>
    public HashSet<int>? SelectedActivityTypes { get; init; }

    public HashSet<DayOfWeek>? AvailableWeekdays { get; init; }

    /// <summary>Saved user session durations; explicit activity answers may refine these for this preview.</summary>
    public IReadOnlyDictionary<int, int>? ActivitySessionDurationsMinutes { get; init; }

    /// <summary>Saved automatic weekly allocations for activities with a user-configurable frequency.</summary>
    public IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek { get; init; }

    /// <summary>Explicit per-activity facts needed by the mixed planner; absent values remain unknown.</summary>
    public IReadOnlyList<ActivityEligibilityInputV020>? ActivityEligibilityEvidence { get; init; }

    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }

    public HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence { get; init; }
}

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

public sealed record HiitEligibilityEvidenceInputV020(
    ActivityTrainingHistoryInputV020? UnderlyingCardioHistory = null,
    IReadOnlyList<HiitAerobicWeekInputV020>? PreviousFourWeeksAerobicEvidence = null,
    bool? ExistingClearanceScreeningSatisfied = null);

public sealed record HiitAerobicWeekInputV020(
    int WeekOffsetFromCurrent,
    int QualifyingManageableAerobicSessionCount);

public sealed record ActivityTrainingHistoryInputV020(
    string ActivityId,
    int? ConsistentParticipationWeeks,
    decimal? AverageSessionsPerWeekInRecentFourWeeks,
    DateOnly? LastCompletedSessionDate,
    bool IsSelfReported);

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
    public IReadOnlySet<int>? SelectedActivityTypes { get; init; }
    public IReadOnlySet<int>? ExcludedExerciseIds { get; init; }
    public IReadOnlySet<DayOfWeek>? AvailableWeekdays { get; init; }
    public IReadOnlyDictionary<int, int>? ActivitySessionDurationsMinutes { get; init; }
    public IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek { get; init; }
    public TrainingExperienceLevel? ProfileTrainingExperience { get; init; }
    public IReadOnlyDictionary<int, int>? GoalPriorityByExerciseId { get; init; }
    public IReadOnlyDictionary<int, IReadOnlySet<string>>? MuscleGroupsByExerciseId { get; init; }
    public int? ProfileVersion { get; init; }
    public string? InjuryFingerprint { get; init; }
    public Guid? ActivePlanId { get; init; }
    public Guid? ActiveRevisionId { get; init; }
    public string? HistoryFingerprint { get; init; }
    public long TimeZoneVersion { get; init; }
    public long ExperienceVersion { get; init; }
    public long AnswerVersion { get; init; }
    public SavedActivityAnswersDto? SavedAnswers { get; init; }
}

public interface IStrengthPlanPreviewSourceV020
{
    Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}
