using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
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
        TodaysWorkoutSummary workoutSummary)
    {
        var readinessScore = CalculateReadinessScore(phase, caloriesProgress, workoutDaysPerWeek);
        var recoveryScore = CalculateRecoveryScore(phase, workoutSummary);

        return new DashboardSupportSummary(
            BuildCycleInsightText(prediction),
            BuildHydrationTarget(weight),
            "target",
            BuildSleepGoal(workoutDaysPerWeek),
            readinessScore,
            BuildReadinessLabel(readinessScore),
            recoveryScore,
            BuildRecoveryLabel(recoveryScore));
    }

    private static int CalculateReadinessScore(
        CyclePhase phase,
        float caloriesProgress,
        int workoutDaysPerWeek)
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
        return Math.Clamp(72 + phaseAdjustment + nutritionAdjustment + trainingAdjustment, 0, 100);
    }

    private static int CalculateRecoveryScore(
        CyclePhase phase,
        TodaysWorkoutSummary workoutSummary)
    {
        var phaseAdjustment = phase switch
        {
            CyclePhase.Menstrual => -4,
            CyclePhase.Follicular => 5,
            CyclePhase.Ovulatory => 3,
            CyclePhase.Luteal => -2,
            _ => 0
        };

        var completedWorkoutLoad = workoutSummary.SessionProgressText == "COMPLETED" ? -8 : 4;
        return Math.Clamp(78 + phaseAdjustment + completedWorkoutLoad, 0, 100);
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

    private static string BuildHydrationTarget(float? weight)
    {
        var liters = weight is > 0f
            ? Math.Clamp(weight.Value * 0.035f, 2f, 3.8f)
            : 2.5f;

        return $"{liters:N1} L";
    }

    private static string BuildSleepGoal(int workoutDaysPerWeek) =>
        workoutDaysPerWeek >= 4 ? "8h" : "7.5h";
}
