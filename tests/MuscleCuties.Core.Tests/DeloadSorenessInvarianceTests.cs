using System.Reflection;
using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Ratified amendment A-8: ordinary soreness is not a recovery input. These tests fail if a soreness
/// member reappears on the recovery evidence the deload rule consumes, or if the deload outcome starts
/// depending on it again.
/// </summary>
public sealed class DeloadSorenessInvarianceTests
{
    [Fact]
    public void Underperformance_with_good_sleep_and_energy_does_not_trigger_a_deload()
    {
        var baseline = DeloadTriggerRule.Evaluate(History(sleep: 4m, energy: 4m));

        Assert.False(baseline.IsDeloadTriggered);
    }

    [Fact]
    public void Poor_sleep_and_underperformance_still_trigger_a_deload()
    {
        var result = DeloadTriggerRule.Evaluate(History(sleep: 1m, energy: 4m));

        Assert.True(result.IsDeloadTriggered);
        Assert.True(result.SetReductionFraction > 0m);
    }

    [Fact]
    public void Recovery_evidence_records_carry_no_soreness_input()
    {
        AssertNoSorenessMember<WeeklyPerformanceAndRecoveryRecord>();
        AssertNoSorenessMember<ObservedRecovery>();
    }

    private static void AssertNoSorenessMember<T>() =>
        Assert.DoesNotContain(typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Soreness", StringComparison.OrdinalIgnoreCase));

    private static WeeklyPerformanceAndRecoveryRecord[] History(decimal sleep, decimal energy) =>
    [
        new(1, CompoundUnderperformed: true, AverageSleepQuality: sleep, AverageEnergy: energy),
        new(2, CompoundUnderperformed: true, AverageSleepQuality: sleep, AverageEnergy: energy)
    ];
}
