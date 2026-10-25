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

    private static int CalculateReadinessScore(
        CyclePhase phase,
        float caloriesProgress,
        int workoutDaysPerWeek,
        HealthWeeklySummary? healthSummary,
        int? localSteps = null,
        double? localAverageSleepHours = null)
    {
        var phaseAdjustment = phase switch
        {
            CyclePhase.Menstrual => -10,
            CyclePhase.Follicular => 8,
            CyclePhase.Ovulatory => 12,
            CyclePhase.Luteal => -4,
            _ => 0
        };

        var nutritionAdjustment = caloriesProgress switch
        {
            >= 0.75f and <= 1.15f => 6,
            < 0.4f => -8,
            > 1.35f => -4,
            _ => 0
        };

        var trainingAdjustment = workoutDaysPerWeek >= 5 ? -3 : workoutDaysPerWeek >= 3 ? 2 : 0;

        var effectiveSteps = healthSummary?.HasMovementData == true
            ? healthSummary.AverageSteps
            : localSteps.GetValueOrDefault();

        var movementAdjustment = effectiveSteps switch
        {
            >= 11000 => 3,
            >= 8000 => 2,
            > 0 and < 4000 => -4,
            _ => 0
        };

        var effectiveSleep = healthSummary?.HasSleepData == true
            ? healthSummary.AverageSleepHours
            : localAverageSleepHours.GetValueOrDefault();

        var sleepAdjustment = effectiveSleep switch
        {
            >= 8 => 5,
            >= 7 => 2,
            > 0 and < 6 => -8,
            _ => 0
        };

        // Absolute heart rate and activity are weak signals without a personal baseline.
        // Use them only for small conservative reductions, never as proof of high readiness.
        var heartAdjustment = healthSummary?.RestingHeartRate switch
        {
            > 100 => -6,
            > 90 => -3,
            _ => 0
        };
        var activeEnergyAdjustment = healthSummary?.AverageActiveEnergyKcal switch
        {
            > 900 => -3,
            > 650 => -1,
            _ => 0
        };

        return Math.Clamp(
            72 + phaseAdjustment + nutritionAdjustment + trainingAdjustment + movementAdjustment + sleepAdjustment +
            heartAdjustment + activeEnergyAdjustment,
            0,
            100);
    }

    private static int CalculateRecoveryScore(
        CyclePhase phase,
        float caloriesProgress,
        TodaysWorkoutSummary workoutSummary,
        HealthWeeklySummary? healthSummary,
        double? localAverageSleepHours = null)
    {
        var phaseAdjustment = phase switch
        {
            CyclePhase.Menstrual => -4,
            CyclePhase.Follicular => 5,
            CyclePhase.Ovulatory => 3,
            CyclePhase.Luteal => -2,
            _ => 0
        };

        var completedWorkoutLoad = string.Equals(
            workoutSummary.SessionProgressText,
            "Completed",
            StringComparison.OrdinalIgnoreCase)
            ? -8
            : 4;
        var nutritionAdjustment = caloriesProgress switch
        {
            >= 0.75f and <= 1.15f => 4,
            < 0.45f => -7,
            _ => 0
        };

        var effectiveSleep = healthSummary?.HasSleepData == true
            ? healthSummary.AverageSleepHours
            : localAverageSleepHours.GetValueOrDefault();

        var sleepAdjustment = effectiveSleep switch
        {
            >= 8.0 => 8,
            >= 7.0 => 4,
            > 0 and < 6.0 => -10,
            _ => 0
        };

        return Math.Clamp(
            78 + phaseAdjustment + completedWorkoutLoad + nutritionAdjustment + sleepAdjustment,
            0,
            100);
    }

    private static string BuildReadinessLabel(int score)
    {
        return score switch
        {
            >= 85 => "Strong training day",
            >= 70 => "Steady energy",
            >= 55 => "Keep it moderate",
            _ => "Prioritize gentle movement"
        };
    }

    private static string BuildRecoveryLabel(int score)
    {
        return score switch
        {
            >= 85 => "Well recovered",
            >= 70 => "Mostly recovered",
            >= 55 => "Needs care",
            _ => "Rest comes first"
        };
    }

    private static string BuildHydrationTarget(float? weight, HealthWeeklySummary? healthSummary,
        int? localSteps = null)
    {
        if (weight is not > 0f || !float.IsFinite(weight.Value))
            return "Unavailable";
        var liters = Math.Clamp(weight.Value * 0.035f, 2f, 3.8f);

        var effectiveSteps = healthSummary?.HasMovementData == true
            ? healthSummary.AverageSteps
            : localSteps.GetValueOrDefault();

        if (effectiveSteps >= 10000)
            liters = Math.Clamp(liters + 0.2f, 2f, 4.2f);

        return $"{liters:N1} L";
    }

    private static string BuildSleepGoal(
        int workoutDaysPerWeek,
        CyclePhase phase,
        HealthWeeklySummary? healthSummary,
        double? localAverageSleepHours = null)
    {
        if (workoutDaysPerWeek < 0) return "Unavailable";
        var goal = workoutDaysPerWeek >= 4 ? 8d : 7.5d;
        if (phase is CyclePhase.Menstrual or CyclePhase.Luteal)
            goal = Math.Max(goal, 8d);

        var avgSleep = healthSummary is { HasSleepData: true }
            ? healthSummary.AverageSleepHours
            : localAverageSleepHours;

        return avgSleep is > 0d && double.IsFinite(avgSleep.Value)
            ? $"{goal:N1}h target · {avgSleep.Value:N1}h avg"
            : goal % 1d == 0d
                ? $"{goal:N0}h"
                : $"{goal:N1}h";
    }
}
