using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Models.Enums.Cycle;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DashboardTrendSummaryRuleTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 22);

    [Fact]
    public void DashboardPhaseUsesQuizProfileOnlyWhenProjectionIsUnknown()
    {
        Assert.Equal(CyclePhase.Luteal,
            DashboardPhaseSelectionRule.Resolve(CyclePhase.Unknown, CyclePhase.Luteal));
        Assert.Equal(CyclePhase.Menstrual,
            DashboardPhaseSelectionRule.Resolve(CyclePhase.Menstrual, CyclePhase.Luteal));
        Assert.Equal(CyclePhase.Unknown,
            DashboardPhaseSelectionRule.Resolve(CyclePhase.Unknown, CyclePhase.Unknown));
    }

    [Fact]
    public void WeeklyNutritionPrefersDailySummaryAndDoesNotDoubleCountFoodEntries()
    {
        var result = DashboardTrendSummaryRule.BuildWeeklyNutrition(
        [
            new(new DateOnly(2026, 10, 19), 500, 20, 40, 15, true),
            new(new DateOnly(2026, 10, 19), 200, 8, 16, 6, false),
            new(new DateOnly(2026, 10, 20), 300, 12, 25, 9, false),
            new(new DateOnly(2026, 10, 18), 900, 30, 90, 20, true)
        ], Thursday);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 10, 19), result.WeekStart);
        Assert.Equal(800, result.Calories);
        Assert.Equal(32, result.ProteinGrams);
        Assert.Equal(65, result.CarbsGrams);
        Assert.Equal(24, result.FatGrams);
    }

    [Fact]
    public void WeeklyWeightsUsesLastLoggedValueOfEachWeek()
    {
        var result = DashboardTrendSummaryRule.BuildWeeklyWeights(
        [
            new(new DateOnly(2026, 10, 13), 70),
            new(new DateOnly(2026, 10, 16), 69.5),
            new(new DateOnly(2026, 10, 20), 69),
            new(new DateOnly(2026, 10, 21), double.NaN),
            new(new DateOnly(2026, 10, 23), 68)
        ], Thursday);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 10, 12), result[0].WeekStart);
        Assert.Equal(69.5, result[0].WeightKg);
        Assert.Equal(new DateOnly(2026, 10, 19), result[1].WeekStart);
        Assert.Equal(69, result[1].WeightKg);
    }

    [Fact]
    public void WorkoutProgressExcludesRestAndUsesOnlyRecordedMetrics()
    {
        var result = DashboardTrendSummaryRule.BuildWorkoutProgress(
        [
            new(true, 200, 9000, 7200, 50, 100),
            new(false, 75, 1500, 2700, 5, 330),
            new(false, 80, 1700, 3100, 6, 300),
            new(false, -5, null, null, null, null)
        ]);

        Assert.Equal(80, result.HeaviestLiftKg);
        Assert.Equal(1700, result.HighestSessionVolumeKg);
        Assert.Equal(52, result.LongestSessionMinutes);
        Assert.Equal(6, result.LongestDistanceKm);
        Assert.Equal(300, result.FastestPaceSecondsPerKm);
    }
}
