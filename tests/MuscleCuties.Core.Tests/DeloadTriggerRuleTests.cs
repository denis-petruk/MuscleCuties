using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DeloadTriggerRuleTests
{
    [Fact]
    public void Evaluate_NullOrEmptyHistory_ReturnsNoDeload()
    {
        var nullResult = DeloadTriggerRule.Evaluate(null);
        Assert.False(nullResult.IsDeloadTriggered);
        Assert.Equal(0, nullResult.ConsecutiveTriggerWeeks);
        Assert.False(nullResult.RemoveOptionalHardConditioning);

        var emptyResult = DeloadTriggerRule.Evaluate([]);
        Assert.False(emptyResult.IsDeloadTriggered);
        Assert.Equal(0, emptyResult.ConsecutiveTriggerWeeks);
    }

    [Fact]
    public void Evaluate_SingleWeekUnderperformanceAndPoorRecovery_DoesNotTriggerDeload()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, AverageSleepQuality: 1.5m, AverageEnergy: 1.8m, AverageSoreness: 4.5m)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.False(result.IsDeloadTriggered);
        Assert.Equal(1, result.ConsecutiveTriggerWeeks);
        Assert.False(result.RemoveOptionalHardConditioning);
        Assert.False(result.RequiresPostDeloadReassessment);
    }

    [Fact]
    public void Evaluate_TwoConsecutiveWeeksUnderperformanceAndPoorRecovery_TriggersDeload()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, AverageSleepQuality: 1.5m),
            new(WeekIndex: 2, CompoundUnderperformed: true, AverageSleepQuality: 2.0m)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.True(result.IsDeloadTriggered);
        Assert.Equal(2, result.ConsecutiveTriggerWeeks);
        Assert.Equal(DeloadTriggerRule.DefaultDeloadSetReductionFraction, result.SetReductionFraction);
        Assert.Equal(DeloadTriggerRule.DefaultDeloadRirIncrease, result.TargetRirIncrease);
        Assert.True(result.RemoveOptionalHardConditioning);
        Assert.True(result.RequiresPostDeloadReassessment);
    }

    [Fact]
    public void Evaluate_ThreeConsecutiveWeeks_TriggersDeloadWithCountThree()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, ExplicitPoorRecovery: true),
            new(WeekIndex: 2, CompoundUnderperformed: true, ExplicitPoorRecovery: true),
            new(WeekIndex: 3, CompoundUnderperformed: true, ExplicitPoorRecovery: true)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.True(result.IsDeloadTriggered);
        Assert.Equal(3, result.ConsecutiveTriggerWeeks);
    }

    [Fact]
    public void Evaluate_TwoWeeksUnderperformedButGoodRecovery_DoesNotTriggerDeload()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, AverageSleepQuality: 4.5m, AverageEnergy: 4.0m, AverageSoreness: 1.5m),
            new(WeekIndex: 2, CompoundUnderperformed: true, AverageSleepQuality: 4.0m, AverageEnergy: 4.5m, AverageSoreness: 2.0m)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.False(result.IsDeloadTriggered);
        Assert.Equal(0, result.ConsecutiveTriggerWeeks);
    }

    [Fact]
    public void Evaluate_TwoWeeksPoorRecoveryButNormalCompoundPerformance_DoesNotTriggerDeload()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: false, ExplicitPoorRecovery: true),
            new(WeekIndex: 2, CompoundUnderperformed: false, ExplicitPoorRecovery: true)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.False(result.IsDeloadTriggered);
        Assert.Equal(0, result.ConsecutiveTriggerWeeks);
    }

    [Fact]
    public void Evaluate_BreakInStreak_OnlyCountsTrailingConsecutiveWeeks()
    {
        var history = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, ExplicitPoorRecovery: true),
            new(WeekIndex: 2, CompoundUnderperformed: false, ExplicitPoorRecovery: false),
            new(WeekIndex: 3, CompoundUnderperformed: true, ExplicitPoorRecovery: true)
        };

        var result = DeloadTriggerRule.Evaluate(history);

        Assert.False(result.IsDeloadTriggered);
        Assert.Equal(1, result.ConsecutiveTriggerWeeks);
    }

    [Fact]
    public void Evaluate_MissingOrDuplicateWeek_DoesNotTreatRecordsAsConsecutive()
    {
        foreach (var previousWeek in new[] { 1, 3 })
        {
            var history = new List<WeeklyPerformanceAndRecoveryRecord>
            {
                new(previousWeek, CompoundUnderperformed: true, ExplicitPoorRecovery: true),
                new(3, CompoundUnderperformed: true, ExplicitPoorRecovery: true)
            };

            var result = DeloadTriggerRule.Evaluate(history);

            Assert.False(result.IsDeloadTriggered);
            Assert.Equal(1, result.ConsecutiveTriggerWeeks);
        }
    }

    [Fact]
    public void CalculateDeloadSets_ReducesWorkingSetsClampedToMinimumOne()
    {
        Assert.Equal(6, DeloadTriggerRule.CalculateDeloadSets(10, 0.40m));
        Assert.Equal(2, DeloadTriggerRule.CalculateDeloadSets(3, 0.40m));
        Assert.Equal(1, DeloadTriggerRule.CalculateDeloadSets(1, 0.40m));
        Assert.Equal(1, DeloadTriggerRule.CalculateDeloadSets(0, 0.40m));
    }

    [Fact]
    public void CalculateDeloadRir_IncreasesTargetRirClampedToMax()
    {
        Assert.Equal(3, DeloadTriggerRule.CalculateDeloadRir(2, 1));
        Assert.Equal(4, DeloadTriggerRule.CalculateDeloadRir(2, 2));
        Assert.Equal(5, DeloadTriggerRule.CalculateDeloadRir(4, 2));
    }
}
