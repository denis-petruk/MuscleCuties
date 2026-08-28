using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Workout.Planning;

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
        HealthWeeklySummary? healthSummary = null)
    {
        var readinessScore = CalculateReadinessScore(phase, caloriesProgress, workoutDaysPerWeek, healthSummary);
        var recoveryScore = CalculateRecoveryScore(phase, caloriesProgress, workoutSummary, healthSummary);

        return new DashboardSupportSummary(
            BuildCycleInsightText(prediction),
            BuildHydrationTarget(weight, healthSummary),
            "target",
            BuildSleepGoal(workoutDaysPerWeek, healthSummary),
            readinessScore,
            BuildReadinessLabel(readinessScore),
            recoveryScore,
            BuildRecoveryLabel(recoveryScore));
    }

    private static int CalculateReadinessScore(
        CyclePhase phase,
        float caloriesProgress,
        int workoutDaysPerWeek,
        HealthWeeklySummary? healthSummary)
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
        var movementAdjustment = healthSummary?.AverageSteps switch
        {
            >= 11000 => 3,
            >= 8000 => 2,
            > 0 and < 4000 => -4,
            _ => 0
        };
        var sleepAdjustment = healthSummary?.SleepQualityScore switch
        {
            >= 85 => 5,
            >= 72 => 2,
            > 0 and < 55 => -8,
            _ => 0
        };

        return Math.Clamp(
            72 + phaseAdjustment + nutritionAdjustment + trainingAdjustment + movementAdjustment + sleepAdjustment,
            0,
            100);
    }

    private static int CalculateRecoveryScore(
        CyclePhase phase,
        float caloriesProgress,
        TodaysWorkoutSummary workoutSummary,
        HealthWeeklySummary? healthSummary)
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
        var sleepAdjustment = healthSummary?.AverageSleepHours switch
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

    private static string BuildReadinessLabel(int score) => score switch
    {
        >= 85 => "Strong training day",
        >= 70 => "Steady energy",
        >= 55 => "Keep it moderate",
        _ => "Prioritize gentle movement"
    };

    private static string BuildRecoveryLabel(int score) => score switch
    {
        >= 85 => "Well recovered",
        >= 70 => "Mostly recovered",
        >= 55 => "Needs care",
        _ => "Rest comes first"
    };

    private static string BuildCycleInsightText(CyclePrediction prediction)
    {
        if (!prediction.HasActiveCycle)
            return "Start tracking to personalize cycle predictions";

        if (prediction.IsPeriodDue)
            return "Period is predicted around now";

        var nextPeriod = prediction.PredictedNextPeriodDate?.ToString("MMM d");
        var ovulation = prediction.PredictedOvulationDate?.ToString("MMM d");
        return nextPeriod is null
            ? $"Day {prediction.CurrentDay} of predicted {prediction.PredictedCycleLength}"
            : $"Next period in {prediction.DaysUntilPeriod}d · Ovulation {ovulation}";
    }

    private static string BuildHydrationTarget(float? weight, HealthWeeklySummary? healthSummary)
    {
        var liters = weight is > 0f
            ? Math.Clamp(weight.Value * 0.035f, 2f, 3.8f)
            : 2.5f;

        if (healthSummary?.AverageSteps >= 10000)
            liters = Math.Clamp(liters + 0.2f, 2f, 4.2f);

        return $"{liters:N1} L";
    }

    private static string BuildSleepGoal(int workoutDaysPerWeek, HealthWeeklySummary? healthSummary)
    {
        var goal = workoutDaysPerWeek >= 4 ? 8d : 7.5d;
        return healthSummary is { HasSleepData: true }
            ? $"{goal:N1}h target · {healthSummary.AverageSleepHours:N1}h avg"
            : goal % 1d == 0d ? $"{goal:N0}h" : $"{goal:N1}h";
    }
}
