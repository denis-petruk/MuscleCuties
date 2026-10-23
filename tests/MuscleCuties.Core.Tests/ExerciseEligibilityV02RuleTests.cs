using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ExerciseEligibilityV02RuleTests
{
    private const int FirstExerciseId = 101;
    private const int SecondExerciseId = 102;
    private const string CatalogVersion = "synthetic-catalog-v1";
    private const decimal SetupMinutes = 1m;

    [Fact]
    public void Reviewed_Eligible_Candidates_Use_Stable_Id_Order()
    {
        var result = ExerciseEligibilityV02Rule.Filter(
            [Candidate(SecondExerciseId), Candidate(FirstExerciseId)], Context());

        Assert.Equal(ExerciseEligibilityStatus.Eligible, result.Status);
        Assert.Equal([FirstExerciseId, SecondExerciseId],
            result.EligibleCandidates.Select(candidate => candidate.ExerciseId));
    }

    [Fact]
    public void Missing_Or_Unreviewed_Safety_Metadata_Is_Ineligible()
    {
        var missing = Candidate(FirstExerciseId) with { DemandTags = null };
        var unreviewed = Candidate(SecondExerciseId) with { RestrictionMappingsReviewed = false };

        var result = ExerciseEligibilityV02Rule.Filter([missing, unreviewed], Context());

        Assert.Equal(ExerciseEligibilityStatus.Blocked, result.Status);
        Assert.Empty(result.EligibleCandidates);
        Assert.Contains(result.Decisions, decision =>
            decision.Reasons.Contains(ExerciseEligibilityReason.MissingCatalogMetadata));
        Assert.Contains(result.Decisions, decision =>
            decision.Reasons.Contains(ExerciseEligibilityReason.UnreviewedCatalogMetadata));
    }

    [Fact]
    public void Explicit_Exclusion_Is_Reported_Even_When_Other_Review_Metadata_Is_Missing()
    {
        var excludedId = FirstExerciseId;
        var incomplete = Candidate(excludedId) with { DemandTags = null, DemandTagsReviewed = false };

        var result = ExerciseEligibilityV02Rule.Filter([incomplete],
            Context(excludedIds: new HashSet<int> { excludedId }));

        Assert.Equal(ExerciseEligibilityStatus.Blocked, result.Status);
        Assert.Contains(ExerciseEligibilityReason.UserExcluded, result.Decisions.Single().Reasons);
        Assert.Contains(ExerciseEligibilityReason.MissingCatalogMetadata, result.Decisions.Single().Reasons);
        Assert.Contains(ExerciseEligibilityReason.UnreviewedCatalogMetadata, result.Decisions.Single().Reasons);
    }

    [Fact]
    public void Explicit_Exclusion_Restriction_Equipment_And_Skill_Are_Hard_Filters()
    {
        var excluded = ExerciseEligibilityV02Rule.Filter([Candidate(FirstExerciseId)],
            Context(excludedIds: new HashSet<int> { FirstExerciseId }));
        var restricted = ExerciseEligibilityV02Rule.Filter([Candidate(FirstExerciseId)],
            Context(blockedTags: new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling }));
        var equipment = ExerciseEligibilityV02Rule.Filter(
            [Candidate(FirstExerciseId) with
                { RequiredEquipment = new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Barbell } }],
            Context());
        var skill = ExerciseEligibilityV02Rule.Filter(
                [Candidate(FirstExerciseId) with
                    { SupportedExperiences = new HashSet<ActivityExperienceV020> { ActivityExperienceV020.Returning } }],
            Context());

        Assert.Contains(ExerciseEligibilityReason.UserExcluded, excluded.Decisions.Single().Reasons);
        Assert.Contains(ExerciseEligibilityReason.ExplicitRestrictionConflict, restricted.Decisions.Single().Reasons);
        Assert.Contains(ExerciseEligibilityReason.EquipmentUnavailable, equipment.Decisions.Single().Reasons);
        Assert.Contains(ExerciseEligibilityReason.UnsupportedExperience, skill.Decisions.Single().Reasons);
        Assert.All(new[] { excluded, restricted, equipment, skill },
            result => Assert.Equal(ExerciseEligibilityStatus.Blocked, result.Status));
    }

    [Fact]
    public void Body_Zone_Alone_Requires_Individual_Review()
    {
        var result = ExerciseEligibilityV02Rule.Filter([Candidate(FirstExerciseId)],
            Context(hasBodyZoneLimitation: true));

        Assert.Equal(ExerciseEligibilityStatus.NeedsIndividualReview, result.Status);
        Assert.Empty(result.EligibleCandidates);
        Assert.Contains(ExerciseEligibilityReason.BodyZoneNeedsIndividualReview,
            result.Decisions.Single().Reasons);
    }

    [Fact]
    public void Unknown_Experience_Is_Allowed_Only_When_Explicitly_Reviewed_For_It()
    {
        var unknownContext = Context() with { Experience = ActivityExperienceV020.Unknown };
        var unsupported = ExerciseEligibilityV02Rule.Filter([Candidate(FirstExerciseId)], unknownContext);
        var supportedCandidate = Candidate(SecondExerciseId) with
        {
            SupportedExperiences = new HashSet<ActivityExperienceV020> { ActivityExperienceV020.Unknown }
        };
        var supported = ExerciseEligibilityV02Rule.Filter([supportedCandidate], unknownContext);
        var assessed = ExerciseEligibilityV02Rule.Filter([supportedCandidate],
            unknownContext with { UnknownExperienceAssessmentCompleted = true });

        Assert.Equal(ExerciseEligibilityStatus.Blocked, unsupported.Status);
        Assert.Contains(ExerciseEligibilityReason.UnsupportedExperience, unsupported.Decisions.Single().Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, supported.Status);
        Assert.Contains(ExerciseEligibilityReason.UnknownExperienceNeedsAssessment,
            supported.Decisions.Single().Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Eligible, assessed.Status);
    }

    [Fact]
    public void Conditional_Review_Requires_Exercise_Specific_Acknowledgment()
    {
        var candidate = Candidate(FirstExerciseId) with
        {
            ExperienceConditionsV020 = "Review setup and demonstrated range.",
            ReviewConditionsV020 = "Use only a controlled, tolerated range."
        };
        var missing = ExerciseEligibilityV02Rule.Filter([candidate], Context());
        var acknowledged = ExerciseEligibilityV02Rule.Filter([candidate],
            Context() with { AcknowledgedExerciseConditionIds = new HashSet<int> { FirstExerciseId } });

        Assert.Equal(ExerciseEligibilityStatus.Blocked, missing.Status);
        Assert.Contains(ExerciseEligibilityReason.ReviewConditionsNeedAcknowledgment,
            missing.Decisions.Single().Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Eligible, acknowledged.Status);
    }

    [Fact]
    public void Missing_Restriction_Mapping_Remains_Blocked_Even_When_Demand_Is_Assessed_Empty()
    {
        var candidate = Candidate(FirstExerciseId) with
        {
            DemandTags = new HashSet<ActivityDemandTag>(),
            DemandTagsReviewed = true,
            RestrictionMappingsReviewed = false
        };
        var result = ExerciseEligibilityV02Rule.Filter([candidate], Context());

        Assert.Equal(ExerciseEligibilityStatus.Blocked, result.Status);
        Assert.Contains(ExerciseEligibilityReason.UnreviewedCatalogMetadata,
            result.Decisions.Single().Reasons);
    }

    [Fact]
    public void Owner_Cleared_Mapping_Uses_Existing_Contraindication_Flags()
    {
        var injury = InjuryFlag.Knee;
        var context = Context() with { ActiveInjuryFlags = injury };
        var cleared = ExerciseEligibilityV02Rule.Filter([Candidate(FirstExerciseId)], context);
        var conflict = ExerciseEligibilityV02Rule.Filter(
            [Candidate(FirstExerciseId) with { Contraindications = injury }], context);

        Assert.Equal(ExerciseEligibilityStatus.Eligible, cleared.Status);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, conflict.Status);
        Assert.Contains(ExerciseEligibilityReason.ExplicitRestrictionConflict,
            conflict.Decisions.Single().Reasons);
    }

    [Fact]
    public void No_Catalog_Candidate_Returns_Blocked_Reason()
    {
        var result = ExerciseEligibilityV02Rule.Filter([], Context());

        Assert.Equal(ExerciseEligibilityStatus.Blocked, result.Status);
        Assert.Contains(ExerciseEligibilityReason.NoEligibleExercise,
            result.Decisions.Single().Reasons);
    }

    [Fact]
    public void Legacy_Substitute_Id_List_Does_Not_Grant_Automatic_Equivalence()
    {
        var original = Candidate(FirstExerciseId) with
        {
            ReviewedSubstituteExerciseIds = new HashSet<int> { SecondExerciseId }
        };
        var substitute = Candidate(SecondExerciseId);

        var legacy = ExerciseEligibilityV02Rule.EvaluateSubstitution(original, substitute, Context());
        var unmapped = ExerciseEligibilityV02Rule.EvaluateSubstitution(
            original with { ReviewedSubstituteExerciseIds = null }, substitute, Context());

        Assert.Equal(ExerciseEligibilityStatus.Blocked, legacy.Status);
        Assert.Contains(ExerciseEligibilityReason.MissingReviewedSubstitutionMapping, legacy.Reasons);
        Assert.Contains(ExerciseEligibilityReason.MissingReviewedSubstitutionMapping, unmapped.Reasons);
    }

    [Fact]
    public void Directed_Substitution_Decisions_Keep_Manual_And_Rejected_Pairs_Out_Of_Automatic_Flow()
    {
        var target = Candidate(SecondExerciseId);
        var original = Candidate(FirstExerciseId) with
        {
            DirectedSubstitutionReviewsV020 =
            [new(SecondExerciseId, DirectedSubstitutionCategoryV020.CloseProgrammingAlternative,
                "Recheck setup and establish a separate load.")]
        };
        var closeUnacknowledged = ExerciseEligibilityV02Rule.EvaluateSubstitution(
            original, target, Context());
        var closeAcknowledged = ExerciseEligibilityV02Rule.EvaluateSubstitution(original, target,
            Context() with { AcknowledgedSubstitutionPairKeys = new HashSet<string>
                { $"{FirstExerciseId}:{SecondExerciseId}" } });
        var closeRestricted = ExerciseEligibilityV02Rule.EvaluateSubstitution(original, target,
            Context(blockedTags: new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling }) with
            {
                AcknowledgedSubstitutionPairKeys = new HashSet<string>
                    { $"{FirstExerciseId}:{SecondExerciseId}" }
            });
        var manual = ExerciseEligibilityV02Rule.EvaluateSubstitution(original with
        {
            DirectedSubstitutionReviewsV020 =
            [new(SecondExerciseId, DirectedSubstitutionCategoryV020.ManualProgrammingAlternative,
                "Change the slot and dose manually.")]
        }, target, Context());
        var rejected = ExerciseEligibilityV02Rule.EvaluateSubstitution(original with
        {
            DirectedSubstitutionReviewsV020 =
            [new(SecondExerciseId, DirectedSubstitutionCategoryV020.RejectedAutomaticSubstitution,
                "Not an equivalent replacement.")]
        }, target, Context());
        var reverse = ExerciseEligibilityV02Rule.EvaluateSubstitution(target, original, Context());

        Assert.Equal(ExerciseEligibilityStatus.NeedsIndividualReview, closeUnacknowledged.Status);
        Assert.Equal(ExerciseEligibilityStatus.Eligible, closeAcknowledged.Status);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, closeRestricted.Status);
        Assert.Contains(ExerciseEligibilityReason.ExplicitRestrictionConflict, closeRestricted.Reasons);
        Assert.Equal(ExerciseEligibilityStatus.NeedsIndividualReview, manual.Status);
        Assert.Contains(ExerciseEligibilityReason.ManualProgrammingChangeRequired, manual.Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, rejected.Status);
        Assert.Contains(ExerciseEligibilityReason.RejectedReviewedSubstitution, rejected.Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, reverse.Status);
    }

    private static ReviewedExerciseCandidate Candidate(int id) => new(
        id, CatalogVersion, MovementPattern.HorizontalPull,
        new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
        new HashSet<ActivityExperienceV020> { ActivityExperienceV020.Beginner },
        new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling },
        true, true, new HashSet<ActivityDemandTag>(),
        new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
        SetupMinutes, new HashSet<int>(), MuscleCuties.Core.Models.Enums.Workout.InjuryFlag.None);

    private static ExerciseEligibilityContext Context(
        IReadOnlySet<int>? excludedIds = null,
        IReadOnlySet<ActivityDemandTag>? blockedTags = null,
        bool hasBodyZoneLimitation = false) => new(
        new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
        ActivityExperienceV020.Beginner,
        excludedIds ?? new HashSet<int>(),
        new HashSet<MovementPattern>(),
        blockedTags ?? new HashSet<ActivityDemandTag>(),
        hasBodyZoneLimitation);
}
