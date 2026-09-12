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
}
