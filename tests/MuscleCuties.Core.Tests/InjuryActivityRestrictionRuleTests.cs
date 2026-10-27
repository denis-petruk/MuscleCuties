using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryActivityRestrictionRuleTests
{
    private static readonly DateOnly InjuryDate = new(2026, 1, 1);

    [Fact]
    public void AcuteKneeAndShoulderRestrictionsCombine()
    {
        Injury[] injuries =
        [
            new(InjuryFlag.Knee, InjuryStatus.Acute, InjuryDate),
            new(InjuryFlag.Shoulder, InjuryStatus.Acute, InjuryDate)
        ];

        var blocked = InjuryActivityRestrictionRule.GetBlockedActivities(injuries);

        Assert.Contains(WorkoutActivityType.Hiit, blocked);
        Assert.Contains(WorkoutActivityType.Running, blocked);
        Assert.Contains(WorkoutActivityType.RockClimbing, blocked);
        Assert.Contains(WorkoutActivityType.Swimming, blocked);
    }

    [Fact]
    public void ClearedAndMissingInjuriesDoNotBlockActivities()
    {
        Injury[] injuries = [new(InjuryFlag.Knee, InjuryStatus.Cleared, InjuryDate)];

        Assert.Empty(InjuryActivityRestrictionRule.GetBlockedActivities(injuries));
        Assert.Empty(InjuryActivityRestrictionRule.GetBlockedActivities(null));
    }
}
