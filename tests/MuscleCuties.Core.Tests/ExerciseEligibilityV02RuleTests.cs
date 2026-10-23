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

        Assert.Equal(ExerciseEligibilityStatus.Blocked, unsupported.Status);
        Assert.Contains(ExerciseEligibilityReason.UnsupportedExperience, unsupported.Decisions.Single().Reasons);
        Assert.Equal(ExerciseEligibilityStatus.Eligible, supported.Status);
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
    public void Substitution_Requires_Reviewed_Equivalence_And_Rechecks_Restrictions()
    {
        var original = Candidate(FirstExerciseId) with
        {
            ReviewedSubstituteExerciseIds = new HashSet<int> { SecondExerciseId }
        };
        var substitute = Candidate(SecondExerciseId);

        var allowed = ExerciseEligibilityV02Rule.EvaluateSubstitution(original, substitute, Context());
        var blocked = ExerciseEligibilityV02Rule.EvaluateSubstitution(original, substitute,
            Context(blockedTags: new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling }));
        var unmapped = ExerciseEligibilityV02Rule.EvaluateSubstitution(
            original with { ReviewedSubstituteExerciseIds = null }, substitute, Context());

        Assert.Equal(ExerciseEligibilityStatus.Eligible, allowed.Status);
        Assert.Equal(ExerciseEligibilityStatus.Blocked, blocked.Status);
        Assert.Contains(ExerciseEligibilityReason.ExplicitRestrictionConflict, blocked.Reasons);
        Assert.Contains(ExerciseEligibilityReason.MissingReviewedSubstitutionMapping, unmapped.Reasons);
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
