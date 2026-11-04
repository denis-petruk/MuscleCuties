using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ReviewedInjuryCandidateSelectionRuleTests
{
    private const int Original = 101;
    private const int Alternative = 102;
    private const int Site = 4;
    private const int Sets = 3;
    private const int Repetitions = 8;
    private static readonly DateOnly Clearance = new(2026, 10, 1);
    private static readonly DateOnly Effective = new(2026, 10, 7);
    private static readonly InjuryReviewScope Scope = new("catalog", "policy", "template", "version", 0, "slot", Site, "phase");
    private static ReviewedInjurySelectionInput Input() => new(Original, false, Effective, [new(false, Clearance)], true, true,
        [Scope], "universe", [Original, Alternative],
        [new(Scope, "universe", true, [new(Original, ReviewedCandidateEligibility.Eligible, Sets, Sets, Repetitions, Repetitions, false, null),
            new(Alternative, ReviewedCandidateEligibility.Ineligible, Sets, Sets, Repetitions, Repetitions, false, null)])],
        [new(Original, Sets, Repetitions, null, false, true, true, true)]);

    [Fact]
    public void OriginalRemainsOptionalDuringClearanceWindow() =>
        Assert.Equal(ReviewedCandidateSelectionStatus.OriginalOptional, ReviewedInjuryCandidateSelectionRule.Evaluate(Input()).Status);

    [Fact]
    public void MissingCandidateCoverageAndUnknownReviewBlock()
    {
        var input = Input();
        var review = input.Reviews.Single();
        Assert.Equal(ReviewedCandidateSelectionStatus.Blocked, ReviewedInjuryCandidateSelectionRule.Evaluate(input with
            { Reviews = [review with { Candidates = review.Candidates.Take(1).ToArray() }] }).Status);
        Assert.Equal(ReviewedCandidateSelectionStatus.Blocked, ReviewedInjuryCandidateSelectionRule.Evaluate(input with
            { Reviews = [review with { Candidates = review.Candidates.Select(candidate => candidate with { Eligibility = ReviewedCandidateEligibility.Unknown }).ToArray() }] }).Status);
    }

    [Fact]
    public void ReviewedLoadRequiresKnownMeasuredValue()
    {
        var input = Input();
        var review = input.Reviews.Single();
        var candidates = review.Candidates.Select(candidate => candidate.ExerciseId == Original
            ? candidate with { LoadEvidenceRequired = true, MaximumLoadKg = 20 } : candidate).ToArray();
        Assert.Equal(ReviewedCandidateSelectionStatus.Blocked, ReviewedInjuryCandidateSelectionRule.Evaluate(input with
            { Reviews = [review with { Candidates = candidates }] }).Status);
    }
}
