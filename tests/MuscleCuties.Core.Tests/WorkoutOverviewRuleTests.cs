using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutOverviewRuleTests
{
    [Fact]
    public void Easier_Alternative_Must_Be_Optional_And_Have_A_Valid_Workout_Id()
    {
        Assert.True(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, true, true));
        Assert.False(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, false, true));
        Assert.False(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumNonNegativeCount, true, false));
    }

}
