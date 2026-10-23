using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityExperienceRuleV020Tests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void ClassifiesBeginnerBelowTwelveConsistentWeeks()
    {
        var history = new ActivityTrainingHistoryV020("running", 11, 3m, Today.AddDays(-1), true);

        var result = ActivityExperienceRuleV020.Classify("running", history, Today);

        Assert.Equal(ActivityExperienceV020.Beginner, result.Experience);
        Assert.True(result.IsSelfReported);
    }

    [Fact]
    public void ClassifiesEstablishedAtTwelveWeeksAndTwoSessionsPerWeek()
    {
        var history = new ActivityTrainingHistoryV020("cycling", 12, 2m, Today.AddDays(-2), false);

        var result = ActivityExperienceRuleV020.Classify("cycling", history, Today);

        Assert.Equal(ActivityExperienceV020.Established, result.Experience);
    }

    [Fact]
    public void ReturningTakesPrecedenceAfterTwentyEightDays()
    {
        var history = new ActivityTrainingHistoryV020("swimming", 40, 2m, Today.AddDays(-28), false);

        var result = ActivityExperienceRuleV020.Classify("swimming", history, Today);

        Assert.Equal(ActivityExperienceV020.Returning, result.Experience);
    }

    [Fact]
    public void DoesNotInferExperienceFromDifferentActivityOrMissingEvidence()
    {
        var history = new ActivityTrainingHistoryV020("running", 60, 5m, Today.AddDays(-2), false);

        Assert.Equal(ActivityExperienceV020.Unknown,
            ActivityExperienceRuleV020.Classify("cycling", history, Today).Experience);
        Assert.Equal(ActivityExperienceV020.Unknown,
            ActivityExperienceRuleV020.Classify("running", null, Today).Experience);
    }

    [Fact]
    public void ProductPolicyRecordsTheSpecifiedHiitDuration()
    {
        Assert.Equal(TimeSpan.FromMinutes(21.5), WorkoutTrainingPolicyV020.HiitSessionDuration);
        Assert.Equal("MC-WORKOUT-POLICY", WorkoutTrainingPolicyV020.PolicyId);
        Assert.Equal("0.2.0", WorkoutTrainingPolicyV020.Version);
    }
}
