using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.Core.Services.Dashboard.Planning;

public class DashboardPlanner : IDashboardPlanner
{
    public DashboardSupportSummary BuildSupportSummary(
        CyclePrediction prediction,
        CyclePhase phase,
        float caloriesProgress,
        float? weight,
        int workoutDaysPerWeek,
        TodaysWorkoutSummary workoutSummary,
        HealthWeeklySummary? healthSummary = null,
        int? recordedReadinessScore = null,
        bool hasActiveInjury = false,
        int? localSteps = null,
        double? localAverageSleepHours = null)
    {
        var cycleMetrics = new DashboardCycleMetrics(
            prediction.HasActiveCycle,
            prediction.IsPeriodDue,
            prediction.PredictedNextPeriodDate.HasValue
                ? DateOnly.FromDateTime(prediction.PredictedNextPeriodDate.Value)
                : null,
            prediction.PredictedOvulationDate.HasValue
                ? DateOnly.FromDateTime(prediction.PredictedOvulationDate.Value)
                : null,
            prediction.CurrentDay,
            prediction.PredictedCycleLength,
            prediction.DaysUntilPeriod);

        var healthMetrics = healthSummary is not null
            ? new DashboardHealthMetrics(
                healthSummary.HasMovementData,
                healthSummary.AverageSteps,
                healthSummary.HasSleepData,
                healthSummary.AverageSleepHours,
                healthSummary.RestingHeartRate,
                healthSummary.AverageActiveEnergyKcal)
            : null;

        var result = DashboardSupportRule.Build(
            cycleMetrics,
            phase,
            caloriesProgress,
            weight,
            workoutDaysPerWeek,
            workoutSummary.SessionProgressText,
            healthMetrics,
            recordedReadinessScore,
            hasActiveInjury,
            localSteps,
            localAverageSleepHours);

        return new DashboardSupportSummary(
            result.CycleInsightText,
            result.HydrationTarget,
            result.CalorieTarget,
            result.SleepGoal,
            result.ReadinessScore,
            result.ReadinessLabel,
            result.RecoveryScore,
            result.RecoveryLabel);
    }
}
