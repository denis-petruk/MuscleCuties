using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityExperienceRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void ClassifiesBeginnerBelowTwelveConsistentWeeks()
    {
        var history = new ActivityTrainingHistory("running", 11, 3m, Today.AddDays(-1), true);

        var result = ActivityExperienceRule.Classify("running", history, Today);

        Assert.Equal(ActivityExperience.Beginner, result.Experience);
        Assert.True(result.IsSelfReported);
    }

    [Fact]
    public void ClassifiesEstablishedAtTwelveWeeksAndTwoSessionsPerWeek()
    {
        var history = new ActivityTrainingHistory("cycling", 12, 2m, Today.AddDays(-2), false);

        var result = ActivityExperienceRule.Classify("cycling", history, Today);

        Assert.Equal(ActivityExperience.Established, result.Experience);
    }

    [Fact]
    public void ReturningTakesPrecedenceAfterTwentyEightDays()
    {
        var history = new ActivityTrainingHistory("swimming", 40, 2m, Today.AddDays(-28), false);

        var result = ActivityExperienceRule.Classify("swimming", history, Today);

        Assert.Equal(ActivityExperience.Returning, result.Experience);
    }

    [Fact]
    public void ReturningRequiresBothConsistentWeeksAndTheEstablishedSessionAverage()
    {
        var history = new ActivityTrainingHistory("swimming", 40, 1m, Today.AddDays(-28), false);

        var result = ActivityExperienceRule.Classify("swimming", history, Today);

        Assert.Equal(ActivityExperience.Beginner, result.Experience);
    }

    [Fact]
    public void MissingHistoryMetricRemainsUnknownWithoutProfileFallback()
    {
        var history = new ActivityTrainingHistory("swimming", 40, null, Today.AddDays(-2), false);

        var result = ActivityExperienceRule.Classify("swimming", history, Today);

        Assert.Equal(ActivityExperience.Unknown, result.Experience);
    }

    [Fact]
    public void DoesNotInferExperienceFromDifferentActivityOrMissingEvidence()
    {
        var history = new ActivityTrainingHistory("running", 60, 5m, Today.AddDays(-2), false);

        Assert.Equal(ActivityExperience.Unknown,
            ActivityExperienceRule.Classify("cycling", history, Today).Experience);
        Assert.Equal(ActivityExperience.Unknown,
            ActivityExperienceRule.Classify("running", null, Today).Experience);
    }

    [Fact]
    public void ActivityAndWorkoutProposalRulesOwnTheirValues()
    {
        Assert.Equal(TimeSpan.FromMinutes(21.5), ActivityPrescriptionRule.HiitSessionDuration);
        Assert.Equal("MC-WORKOUT-POLICY", StrengthPlanPreviewRule.PolicyId);
        Assert.Equal("0.2.0", StrengthPlanPreviewRule.Version);
        Assert.Equal("MC-PLANNING-0.2", StrengthPlanPreviewRule.RulesetVersion);
    }
}
