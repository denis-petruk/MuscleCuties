using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Workout.Planning;

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
            caloriesProgress: 0.9f,
            weight: 70f,
            workoutDaysPerWeek: 4,
            TodaysWorkoutSummary.RestDay);

        Assert.Equal("Next period in 18d · Ovulation Aug 16", summary.CycleInsightText);
        Assert.Equal("2.5 L", summary.HydrationConsumed);
        Assert.Equal("8h", summary.SleepGoal);
        Assert.Equal("Strong training day", summary.ReadinessLabel);
        Assert.Equal("Well recovered", summary.RecoveryLabel);
    }
}
