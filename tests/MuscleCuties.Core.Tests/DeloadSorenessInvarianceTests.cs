using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DeloadSorenessInvarianceTests
{
    [Fact]
    public void Ordinary_soreness_does_not_turn_underperformance_into_a_deload()
    {
        var withoutSoreness = History(soreness: 1m, sleep: 4m, energy: 4m);
        var withSoreness = History(soreness: 5m, sleep: 4m, energy: 4m);

        var baseline = DeloadTriggerRule.Evaluate(withoutSoreness);
        var sore = DeloadTriggerRule.Evaluate(withSoreness);

        Assert.False(baseline.IsDeloadTriggered);
        Assert.Equal(baseline, sore);
    }

    [Fact]
    public void Poor_sleep_and_underperformance_still_trigger_deload_independently_of_soreness()
    {
        var withoutSoreness = DeloadTriggerRule.Evaluate(History(soreness: 1m, sleep: 1m, energy: 4m));
        var withSoreness = DeloadTriggerRule.Evaluate(History(soreness: 5m, sleep: 1m, energy: 4m));

        Assert.True(withoutSoreness.IsDeloadTriggered);
        Assert.True(withoutSoreness.SetReductionFraction > 0m);
        Assert.Equal(withoutSoreness, withSoreness);
    }

    private static WeeklyPerformanceAndRecoveryRecord[] History(decimal soreness, decimal sleep, decimal energy) =>
    [
        new(1, CompoundUnderperformed: true, AverageSleepQuality: sleep, AverageEnergy: energy, AverageSoreness: soreness),
        new(2, CompoundUnderperformed: true, AverageSleepQuality: sleep, AverageEnergy: energy, AverageSoreness: soreness)
    ];
}
