using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ActivitySafetyAnswersRuleTests
{
    [Fact]
    public void ReportsAllInitiallyMissingSelectedAnswers()
    {
        var missing = ActivitySafetyAnswersRule.Missing(
            [WorkoutActivityType.Swimming, WorkoutActivityType.RockClimbing, WorkoutActivityType.Yoga], []);
        Assert.Contains(ActivitySafetyAnswersRule.SwimmingFacilityMissing, missing);
        Assert.Contains(ActivitySafetyAnswersRule.ClimbingCompetenceMissing, missing);
        Assert.Contains(ActivitySafetyAnswersRule.YogaTypeMissing, missing);
    }

    [Fact]
    public void AsksDependentAnswersOnlyWhenRequired()
    {
        var swimming = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, true, true, null, null, null, null);
        var climbing = new ActivitySafetyAnswers(WorkoutActivityType.RockClimbing, null, null, null, false, null, null);
        var missing = ActivitySafetyAnswersRule.Missing([swimming.Activity, climbing.Activity], [swimming, climbing]);
        Assert.Contains(ActivitySafetyAnswersRule.SwimmingStyleMissing, missing);
        Assert.DoesNotContain(ActivitySafetyAnswersRule.ClimbingInstructionMissing, missing);
    }
    [Fact]
    public void EstablishedClimberDoesNotRequireInstructionButUnknownHistoryDoes()
    {
        var answer = new ActivitySafetyAnswers(WorkoutActivityType.RockClimbing, null, null, null,
            true, null, null, ActivityExperience.Established);
        Assert.Empty(ActivitySafetyAnswersRule.Missing([answer.Activity], [answer]));
        Assert.Contains(ActivitySafetyAnswersRule.ClimbingInstructionMissing,
            ActivitySafetyAnswersRule.Missing([answer.Activity], [answer with { ClimbingExperience = ActivityExperience.Unknown }]));
    }

    [Fact]
    public void UnknownSafetyNeverClassifiesAsSafe()
    {
        Assert.Equal(ActivitySafetyAnswerState.Unknown, ActivitySafetyAnswersRule.Classify(null));
        Assert.Equal(ActivitySafetyAnswerState.Unsafe, ActivitySafetyAnswersRule.Classify(
            new ActivitySafetyAnswers(WorkoutActivityType.Swimming, false, null, null, null, null, null)));
    }

}
