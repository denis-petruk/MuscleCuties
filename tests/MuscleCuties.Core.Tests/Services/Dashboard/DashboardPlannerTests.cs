using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;

namespace MuscleCuties.Core.Tests.Services.Dashboard;

public class DashboardPlannerTests
{
    private readonly DashboardPlanner _planner = new();

    [Fact]
    public void BuildSupportSummary_UsesCycleNutritionWorkoutAndProfileMetrics()
    {
        var prediction = new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentDay = 10,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            PredictedNextPeriodDate = new DateTime(2026, 8, 30),
            PredictedOvulationDate = new DateTime(2026, 8, 16),
            DaysUntilPeriod = 18
        };

        var summary = _planner.BuildSupportSummary(
            prediction,
            CyclePhase.Follicular,
            0.9f,
            70f,
            4,
            TodaysWorkoutSummary.RestDay);

        Assert.Equal("Next period in 18d · Ovulation Aug 16", summary.CycleInsightText);
        Assert.Equal("2.5 L", summary.HydrationConsumed);
        Assert.Equal("8h", summary.SleepGoal);
        Assert.Equal("Strong training day", summary.ReadinessLabel);
        Assert.Equal("Well recovered", summary.RecoveryLabel);
    }

    [Fact]
    public void BuildSupportSummary_UsesRecordedDailyReadinessWhenAvailable()
    {
        var summary = _planner.BuildSupportSummary(
            new CyclePrediction(),
            CyclePhase.Follicular,
            0.9f,
            70f,
            4,
            TodaysWorkoutSummary.RestDay,
            recordedReadinessScore: 58);

        Assert.Equal(58, summary.ReadinessScore);
        Assert.Equal("Keep it moderate", summary.ReadinessLabel);
    }

    [Fact]
    public void BuildSupportSummary_ActiveInjuryCapsHighScoresAndExplainsWhy()
    {
        var summary = _planner.BuildSupportSummary(
            new CyclePrediction(), CyclePhase.Follicular, 0.9f, 70f, 4,
            TodaysWorkoutSummary.RestDay, recordedReadinessScore: 96, hasActiveInjury: true);

        Assert.Equal(85, summary.ReadinessScore);
        Assert.Equal(85, summary.RecoveryScore);
        Assert.Equal("Limited by injury", summary.ReadinessLabel);
        Assert.Equal("Recovery limited", summary.RecoveryLabel);
    }

    [Theory]
    [InlineData(54)]
    [InlineData(70)]
    [InlineData(85)]
    public void BuildSupportSummary_ActiveInjuryDoesNotRaiseLowerScores(int readiness)
    {
        var withoutInjury = _planner.BuildSupportSummary(
            new CyclePrediction(), CyclePhase.Menstrual, 0f, 70f, 4,
            TodaysWorkoutSummary.RestDay, recordedReadinessScore: readiness);
        var withInjury = _planner.BuildSupportSummary(
            new CyclePrediction(), CyclePhase.Menstrual, 0f, 70f, 4,
            TodaysWorkoutSummary.RestDay, recordedReadinessScore: readiness, hasActiveInjury: true);

        Assert.Equal(withoutInjury.ReadinessScore, withInjury.ReadinessScore);
        Assert.Equal(withoutInjury.RecoveryScore, withInjury.RecoveryScore);
        Assert.Equal(withoutInjury.ReadinessLabel, withInjury.ReadinessLabel);
        Assert.Equal(withoutInjury.RecoveryLabel, withInjury.RecoveryLabel);
    }
}
