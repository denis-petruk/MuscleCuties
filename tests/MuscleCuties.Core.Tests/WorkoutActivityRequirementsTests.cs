using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutActivityRequirementsTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    [Fact]
    public void EstablishedClimberDoesNotNeedInstructionAnswer()
    {
        var activity = WorkoutActivityDto.RockClimbing;
        var history = new ActivityTrainingHistoryInputV020(activity.ToString(), 12, 2m, Today, true);
        var answer = new ActivityEligibilityInputV020(activity, history, HasRopeClimbingCompetence: true);

        var missing = WorkoutActivityRequirements.Missing([activity], [answer], Today);

        Assert.DoesNotContain(WorkoutActivityRequirements.ClimbingInstructionMissing, missing);
    }

    [Fact]
    public void UnknownClimberNeedsInstructionAndYogaNeedsExplicitType()
    {
        var climbing = new ActivityEligibilityInputV020(WorkoutActivityDto.RockClimbing,
            HasRopeClimbingCompetence: true);
        var yoga = new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga);

        var missing = WorkoutActivityRequirements.Missing([climbing.Activity, yoga.Activity], [climbing, yoga], Today);

        Assert.Contains(WorkoutActivityRequirements.ClimbingInstructionMissing, missing);
        Assert.Contains(WorkoutActivityRequirements.YogaTypeMissing, missing);
    }
}
