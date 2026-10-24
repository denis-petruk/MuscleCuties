using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ExerciseEquipmentKind
{
    Bodyweight,
    Dumbbell,
    ResistanceBand,
    Barbell,
    Cable,
    Machine,
    HipThrustBench,
    BackExtension45,
    PullUpBar,
    Kettlebell,
    ClimbingWall
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
    UnknownExperienceNeedsAssessment,
    ReviewConditionsNeedAcknowledgment,
    RejectedReviewedSubstitution,
    ManualProgrammingChangeRequired,
    SubstitutionConditionsNeedAcknowledgment,
    NoEligibleExercise
}

public enum DirectedSubstitutionCategory
{
    CloseProgrammingAlternative,
    ManualProgrammingAlternative,
    RejectedAutomaticSubstitution
}

public sealed record DirectedSubstitutionReview(
    int TargetExerciseId,
    DirectedSubstitutionCategory Category,
    string Conditions);

public sealed record ReviewedExerciseCandidate(
    int ExerciseId,
    string CatalogVersion,
    MovementPattern Pattern,
    IReadOnlySet<ExerciseEquipmentKind>? RequiredEquipment,
    IReadOnlySet<ActivityExperience>? SupportedExperiences,
    IReadOnlySet<ActivityDemandTag>? DemandTags,
    bool DemandTagsReviewed,
    bool RestrictionMappingsReviewed,
    IReadOnlySet<ActivityDemandTag>? ReviewedRestrictionConflictTags,
    IReadOnlySet<ExerciseTimingMethod>? SupportedTimingMethods,
    decimal? SetupMinutes,
    IReadOnlySet<int>? ReviewedSubstituteExerciseIds,
    MuscleCuties.Core.Models.Enums.Workout.InjuryFlag? Contraindications = null,
    string? ExperienceConditions = null,
    string? ReviewConditions = null,
    bool ReviewEvidencePresent = true,
    IReadOnlyList<DirectedSubstitutionReview>? DirectedSubstitutionReviews = null,
    bool UnilateralDoublesTime = false);

public sealed record ExerciseEligibilityContext(
    IReadOnlySet<ExerciseEquipmentKind> AvailableEquipment,
    ActivityExperience Experience,
    IReadOnlySet<int> ExcludedExerciseIds,
    IReadOnlySet<MovementPattern> BlockedMovementPatterns,
    IReadOnlySet<ActivityDemandTag> ExplicitlyBlockedDemandTags,
    bool HasBodyZoneLimitation,
    MuscleCuties.Core.Models.Enums.Workout.InjuryFlag ActiveInjuryFlags =
        MuscleCuties.Core.Models.Enums.Workout.InjuryFlag.None,
    IReadOnlySet<int>? AcknowledgedExerciseConditionIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    IReadOnlySet<string>? AcknowledgedSubstitutionPairKeys = null);

public sealed record ExerciseEligibilityDecision(
    ReviewedExerciseCandidate? Candidate,
    ExerciseEligibilityStatus Status,
    IReadOnlyList<ExerciseEligibilityReason> Reasons);

public sealed record ExerciseSelectionResult(
    ExerciseEligibilityStatus Status,
    IReadOnlyList<ReviewedExerciseCandidate> EligibleCandidates,
    IReadOnlyList<ExerciseEligibilityDecision> Decisions);

/// <summary>Hard filters only; a generic mapping never certifies individual exercise safety.</summary>
public static class ExerciseEligibilityRule
{
    public static ExerciseSelectionResult Filter(
        IReadOnlyList<ReviewedExerciseCandidate>? candidates,
        ExerciseEligibilityContext? context)
    {
        if (context is null || context.AvailableEquipment is null || context.ExcludedExerciseIds is null ||
            context.BlockedMovementPatterns is null || context.ExplicitlyBlockedDemandTags is null ||
            !Enum.IsDefined(context.Experience))
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
        if (original?.DirectedSubstitutionReviews is { } directed)
        {
            var review = substitute is null ? null : directed.FirstOrDefault(pair =>
                pair.TargetExerciseId == substitute.ExerciseId);
            if (review is null)
                return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.Blocked,
                    [ExerciseEligibilityReason.MissingReviewedSubstitutionMapping]);
            if (review.Category == DirectedSubstitutionCategory.RejectedAutomaticSubstitution)
                return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.Blocked,
                    [ExerciseEligibilityReason.RejectedReviewedSubstitution]);
            if (review.Category == DirectedSubstitutionCategory.ManualProgrammingAlternative)
                return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.NeedsIndividualReview,
                    [ExerciseEligibilityReason.ManualProgrammingChangeRequired]);
            var key = $"{original.ExerciseId}:{substitute!.ExerciseId}";
            if (string.IsNullOrWhiteSpace(review.Conditions) ||
                context?.AcknowledgedSubstitutionPairKeys?.Contains(key) != true)
                return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.NeedsIndividualReview,
                    [ExerciseEligibilityReason.SubstitutionConditionsNeedAcknowledgment]);
            return Evaluate(substitute, context);
        }
        return new ExerciseEligibilityDecision(substitute, ExerciseEligibilityStatus.Blocked,
            [ExerciseEligibilityReason.MissingReviewedSubstitutionMapping]);
    }

    private static ExerciseEligibilityDecision Evaluate(
        ReviewedExerciseCandidate? candidate,
        ExerciseEligibilityContext? context)
    {
        if (candidate is null || context is null)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Blocked,
                [ExerciseEligibilityReason.MissingCatalogMetadata]);

        var reasons = new List<ExerciseEligibilityReason>();
        var metadataMissing = !candidate.ReviewEvidencePresent || candidate.ExerciseId <= 0 ||
            string.IsNullOrWhiteSpace(candidate.CatalogVersion) ||
            !Enum.IsDefined(candidate.Pattern) ||
            candidate.RequiredEquipment is null || candidate.RequiredEquipment.Count == 0 ||
            candidate.RequiredEquipment?.Any(kind => !Enum.IsDefined(kind)) == true ||
            candidate.DemandTags is null ||
            candidate.DemandTags?.Any(tag => !Enum.IsDefined(tag)) == true ||
            candidate.ReviewedRestrictionConflictTags is null ||
            candidate.ReviewedRestrictionConflictTags?.Any(tag => !Enum.IsDefined(tag)) == true ||
            candidate.SupportedTimingMethods is null || candidate.SupportedTimingMethods.Count == 0 ||
            candidate.SupportedTimingMethods?.Any(method => !Enum.IsDefined(method)) == true ||
            candidate.SetupMinutes is null || candidate.SetupMinutes < 0 ||
            candidate.SupportedExperiences is null || candidate.SupportedExperiences.Count == 0 ||
            candidate.SupportedExperiences?.Any(experience => !Enum.IsDefined(experience)) == true ||
            candidate.Contraindications is null;
        if (metadataMissing)
            reasons.Add(ExerciseEligibilityReason.MissingCatalogMetadata);
        if (!candidate.DemandTagsReviewed || !candidate.RestrictionMappingsReviewed)
            reasons.Add(ExerciseEligibilityReason.UnreviewedCatalogMetadata);
        if (context.ExcludedExerciseIds.Contains(candidate.ExerciseId))
            reasons.Add(ExerciseEligibilityReason.UserExcluded);
        if (context.BlockedMovementPatterns.Contains(candidate.Pattern) ||
            candidate.DemandTags?.Overlaps(context.ExplicitlyBlockedDemandTags) == true ||
            candidate.ReviewedRestrictionConflictTags?.Overlaps(context.ExplicitlyBlockedDemandTags) == true ||
            candidate.Contraindications is { } contraindications &&
            (contraindications & context.ActiveInjuryFlags) != MuscleCuties.Core.Models.Enums.Workout.InjuryFlag.None)
            reasons.Add(ExerciseEligibilityReason.ExplicitRestrictionConflict);
        if (candidate.RequiredEquipment is not null &&
            !candidate.RequiredEquipment.IsSubsetOf(context.AvailableEquipment))
            reasons.Add(ExerciseEligibilityReason.EquipmentUnavailable);
        if (candidate.SupportedExperiences is not null &&
            !candidate.SupportedExperiences.Contains(context.Experience))
            reasons.Add(ExerciseEligibilityReason.UnsupportedExperience);
        if (context.Experience == ActivityExperience.Unknown &&
            !context.UnknownExperienceAssessmentCompleted)
            reasons.Add(ExerciseEligibilityReason.UnknownExperienceNeedsAssessment);
        if ((!string.IsNullOrWhiteSpace(candidate.ExperienceConditions) ||
             !string.IsNullOrWhiteSpace(candidate.ReviewConditions)) &&
            context.AcknowledgedExerciseConditionIds?.Contains(candidate.ExerciseId) != true)
            reasons.Add(ExerciseEligibilityReason.ReviewConditionsNeedAcknowledgment);
        if (reasons.Count > 0)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Blocked,
                reasons.Distinct().ToArray());

        if (context.HasBodyZoneLimitation)
            return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.NeedsIndividualReview,
                [ExerciseEligibilityReason.BodyZoneNeedsIndividualReview]);

        return new ExerciseEligibilityDecision(candidate, ExerciseEligibilityStatus.Eligible, []);
    }
}
