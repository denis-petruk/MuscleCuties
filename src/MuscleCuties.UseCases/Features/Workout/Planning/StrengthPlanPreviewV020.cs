using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthPlanPreviewRequestV020(
    WorkoutActivityDto Activity,
    DateOnly LocalDate,
    ActivityTrainingHistoryInputV020? ActivityHistory,
    IReadOnlySet<int>? ExcludedExerciseIds = null,
    IReadOnlySet<int>? UserConfirmedFamiliarExerciseIds = null,
    IReadOnlySet<int>? AcknowledgedExerciseConditionIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    IReadOnlySet<int>? UserConfirmedManageableLoadExerciseIds = null,
    IReadOnlySet<string>? AcknowledgedSubstitutionPairKeys = null,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null)
{
    public IReadOnlySet<DayOfWeek>? AvailableWeekdays { get; init; }

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
    bool? IsDemandingYogaContent = null);

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
}

public interface IStrengthPlanPreviewSourceV020
{
    Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}
