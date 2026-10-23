using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ExerciseEquipmentKind
{
    Bodyweight,
    Dumbbell,
    ResistanceBand,
    Barbell,
    Cable,
    Machine
}

public enum ExerciseTimingMethod
{
    StraightSets,
    Rounds,
    Timed
}

public enum ExerciseEligibilityStatus
{
    Eligible,
    NeedsIndividualReview,
    Blocked
}

public enum ExerciseEligibilityReason
{
    MissingCatalogMetadata,
    UnreviewedCatalogMetadata,
    UserExcluded,
    ExplicitRestrictionConflict,
    EquipmentUnavailable,
    UnsupportedExperience,
    BodyZoneNeedsIndividualReview,
    MissingReviewedSubstitutionMapping,
    NoEligibleExercise
}

public sealed record ReviewedExerciseCandidate(
    int ExerciseId,
    string CatalogVersion,
    MovementPattern Pattern,
    IReadOnlySet<ExerciseEquipmentKind>? RequiredEquipment,
    TrainingExperienceLevel MinimumExperience,
    IReadOnlySet<ActivityDemandTag>? DemandTags,
    bool DemandTagsReviewed,
    bool RestrictionMappingsReviewed,
    IReadOnlySet<ActivityDemandTag>? ReviewedRestrictionConflictTags,
    IReadOnlySet<ExerciseTimingMethod>? SupportedTimingMethods,
    decimal? SetupMinutes,
    IReadOnlySet<int>? ReviewedSubstituteExerciseIds);

public sealed record ExerciseEligibilityContext(
    IReadOnlySet<ExerciseEquipmentKind> AvailableEquipment,
    TrainingExperienceLevel Experience,
    IReadOnlySet<int> ExcludedExerciseIds,
    IReadOnlySet<MovementPattern> BlockedMovementPatterns,
    IReadOnlySet<ActivityDemandTag> ExplicitlyBlockedDemandTags,
    bool HasBodyZoneLimitation);

public sealed record ExerciseEligibilityDecision(
    ReviewedExerciseCandidate? Candidate,
    ExerciseEligibilityStatus Status,
    IReadOnlyList<ExerciseEligibilityReason> Reasons);

public sealed record ExerciseSelectionResult(
    ExerciseEligibilityStatus Status,
    IReadOnlyList<ReviewedExerciseCandidate> EligibleCandidates,
    IReadOnlyList<ExerciseEligibilityDecision> Decisions);

/// <summary>Hard filters only; a generic mapping never certifies individual exercise safety.</summary>
public static class ExerciseEligibilityV02Rule
{
    public static ExerciseSelectionResult Filter(
        IReadOnlyList<ReviewedExerciseCandidate>? candidates,
        ExerciseEligibilityContext? context)
    {
        if (context is null || context.AvailableEquipment is null || context.ExcludedExerciseIds is null ||
            context.BlockedMovementPatterns is null || context.ExplicitlyBlockedDemandTags is null ||
            !Enum.IsDefined(context.Experience) || context.Experience == TrainingExperienceLevel.Unknown)
            return new ExerciseSelectionResult(ExerciseEligibilityStatus.Blocked, [],
                [new ExerciseEligibilityDecision(null, ExerciseEligibilityStatus.Blocked,
                    [ExerciseEligibilityReason.MissingCatalogMetadata])]);

        var decisions = (candidates ?? [])
            .OrderBy(candidate => candidate?.ExerciseId)
            .ThenBy(candidate => candidate?.CatalogVersion, StringComparer.Ordinal)
            .Select(candidate => Evaluate(candidate, context))
            .ToList();
        if (decisions.Count == 0)
            decisions.Add(new ExerciseEligibilityDecision(null, ExerciseEligibilityStatus.Blocked,
                [ExerciseEligibilityReason.NoEligibleExercise]));
        var eligible = decisions
            .Where(decision => decision.Status == ExerciseEligibilityStatus.Eligible)
            .Select(decision => decision.Candidate!)
            .ToArray();
        var status = eligible.Length > 0
            ? ExerciseEligibilityStatus.Eligible
            : decisions.Any(decision => decision.Status == ExerciseEligibilityStatus.NeedsIndividualReview)
                ? ExerciseEligibilityStatus.NeedsIndividualReview
                : ExerciseEligibilityStatus.Blocked;
        return new ExerciseSelectionResult(status, eligible, decisions);
    }

    public static ExerciseEligibilityDecision EvaluateSubstitution(
        ReviewedExerciseCandidate? original,
        ReviewedExerciseCandidate? substitute,
        ExerciseEligibilityContext? context)
    {
        if (original?.ReviewedSubstituteExerciseIds is null ||
            substitute is null || !original.ReviewedSubstituteExerciseIds.Contains(substitute.ExerciseId))
            return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.Blocked,
                [ExerciseEligibilityReason.MissingReviewedSubstitutionMapping]);

        return Evaluate(substitute, context);
    }

    private static ExerciseEligibilityDecision Evaluate(
        ReviewedExerciseCandidate? candidate,
        ExerciseEligibilityContext? context)
    {
        if (candidate is null || context is null || candidate.ExerciseId <= 0 ||
            string.IsNullOrWhiteSpace(candidate.CatalogVersion) ||
            !Enum.IsDefined(candidate.Pattern) ||
            candidate.RequiredEquipment is null || candidate.RequiredEquipment.Count == 0 ||
            candidate.RequiredEquipment.Any(kind => !Enum.IsDefined(kind)) ||
            candidate.DemandTags is null || candidate.DemandTags.Count == 0 ||
            candidate.DemandTags.Any(tag => !Enum.IsDefined(tag)) ||
            candidate.ReviewedRestrictionConflictTags is null ||
            candidate.ReviewedRestrictionConflictTags.Any(tag => !Enum.IsDefined(tag)) ||
            candidate.SupportedTimingMethods is null || candidate.SupportedTimingMethods.Count == 0 ||
            candidate.SupportedTimingMethods.Any(method => !Enum.IsDefined(method)) ||
            candidate.SetupMinutes is null || candidate.SetupMinutes < 0 ||
            !Enum.IsDefined(candidate.MinimumExperience) ||
            candidate.MinimumExperience == TrainingExperienceLevel.Unknown)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Blocked,
                [ExerciseEligibilityReason.MissingCatalogMetadata]);

        var reasons = new List<ExerciseEligibilityReason>();
        if (!candidate.DemandTagsReviewed || !candidate.RestrictionMappingsReviewed)
            reasons.Add(ExerciseEligibilityReason.UnreviewedCatalogMetadata);
        if (context.ExcludedExerciseIds.Contains(candidate.ExerciseId))
            reasons.Add(ExerciseEligibilityReason.UserExcluded);
        if (context.BlockedMovementPatterns.Contains(candidate.Pattern) ||
            candidate.DemandTags.Overlaps(context.ExplicitlyBlockedDemandTags) ||
            candidate.ReviewedRestrictionConflictTags.Overlaps(context.ExplicitlyBlockedDemandTags))
            reasons.Add(ExerciseEligibilityReason.ExplicitRestrictionConflict);
        if (!candidate.RequiredEquipment.IsSubsetOf(context.AvailableEquipment))
            reasons.Add(ExerciseEligibilityReason.EquipmentUnavailable);
        if (candidate.MinimumExperience > context.Experience)
            reasons.Add(ExerciseEligibilityReason.UnsupportedExperience);
        if (reasons.Count > 0)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Blocked, reasons);

        if (context.HasBodyZoneLimitation)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.NeedsIndividualReview,
                [ExerciseEligibilityReason.BodyZoneNeedsIndividualReview]);

        return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Eligible, []);
    }
}
