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

    [Fact]
    public void StrengthIsFirstByDefaultAndEitherExplicitOrderIsValid()
    {
        var ordered = WorkoutSessionOrderingRule.Assign([
            new(WorkoutActivityType.Yoga, DayOfWeek.Monday, SyntheticSessionMinutes, false),
            new(WorkoutActivityType.GeneralStrength, DayOfWeek.Monday, SyntheticSessionMinutes, true)]);
        Assert.Equal(WorkoutActivityType.GeneralStrength, ordered[0].Activity);
        Assert.Equal(WorkoutSessionOrderingRule.FirstSessionOrder, ordered[0].DayOrder);
        Assert.True(WorkoutSessionOrderingRule.IsValid([
            ordered[0] with { DayOrder = SecondSessionOrder },
            ordered[1] with { DayOrder = WorkoutSessionOrderingRule.FirstSessionOrder }]));
    }
}
