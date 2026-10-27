using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutSessionOrderingRuleTests
{
    private const int SyntheticSessionMinutes = 20;
    private const int SecondSessionOrder = 2;
    [Fact]
    public void NewPlanCarriesExplicitIndependentPerDaySequence()
    {
        var sessions = new[]
        {
            new MixedActivityScheduledSession(WorkoutActivityType.Yoga, DayOfWeek.Monday, SyntheticSessionMinutes, false),
            new MixedActivityScheduledSession(WorkoutActivityType.Mobility, DayOfWeek.Monday, SyntheticSessionMinutes, false),
            new MixedActivityScheduledSession(WorkoutActivityType.Yoga, DayOfWeek.Tuesday, SyntheticSessionMinutes, false)
        };
        var ordered = WorkoutSessionOrderingRule.Assign(sessions);
        Assert.True(WorkoutSessionOrderingRule.IsValid(ordered));
        Assert.Equal(new int?[] { WorkoutSessionOrderingRule.FirstSessionOrder, SecondSessionOrder,
            WorkoutSessionOrderingRule.FirstSessionOrder }, ordered.Select(session => session.DayOrder));
        Assert.False(WorkoutSessionOrderingRule.IsValid(sessions));
    }
}
