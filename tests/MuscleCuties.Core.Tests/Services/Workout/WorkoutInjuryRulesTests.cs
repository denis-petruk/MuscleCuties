using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout;

public class WorkoutInjuryRulesTests
{
    [Theory]
    [InlineData(InjurySite.Ankle)]
    [InlineData(InjurySite.Metatarsal)]
    public void AcuteFootOrAnkleInjuryBlocksImpactActivities(InjurySite site)
    {
        var blocked = WorkoutInjuryRules.GetBlockedActivities(
            [new Injury(site, InjuryStatus.Acute, DateOnly.FromDateTime(DateTime.Today))]);

        Assert.Contains(WorkoutActivityType.Running, blocked);
        Assert.Contains(WorkoutActivityType.Hiit, blocked);
        Assert.Contains(WorkoutActivityType.RockClimbing, blocked);
    }

    [Fact]
    public void RecoveringAnkleOnlyBlocksHiit()
    {
        var blocked = WorkoutInjuryRules.GetBlockedActivities(
            [new Injury(InjurySite.Ankle, InjuryStatus.Recovering, DateOnly.FromDateTime(DateTime.Today))]);

        Assert.Contains(WorkoutActivityType.Hiit, blocked);
        Assert.DoesNotContain(WorkoutActivityType.Running, blocked);
        Assert.DoesNotContain(WorkoutActivityType.RockClimbing, blocked);
    }

    [Fact]
    public void AcuteShoulderBlocksSwimmingAndClimbing()
    {
        var blocked = WorkoutInjuryRules.GetBlockedActivities(
            [new Injury(InjurySite.Shoulder, InjuryStatus.Acute, DateOnly.FromDateTime(DateTime.Today))]);

        Assert.Contains(WorkoutActivityType.Swimming, blocked);
        Assert.Contains(WorkoutActivityType.RockClimbing, blocked);
    }

    [Fact]
    public void ClearedInjuryDoesNotBlockActivities()
    {
        var blocked = WorkoutInjuryRules.GetBlockedActivities(
            [new Injury(InjurySite.Knee, InjuryStatus.Cleared, DateOnly.FromDateTime(DateTime.Today))]);

        Assert.Empty(blocked);
    }
}
