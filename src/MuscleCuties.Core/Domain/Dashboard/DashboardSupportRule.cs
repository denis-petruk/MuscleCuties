using System.Globalization;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Dashboard;

public sealed record DashboardHealthMetrics(
    bool HasMovementData,
    int AverageSteps,
    bool HasSleepData,
    double AverageSleepHours,
    int? RestingHeartRate,
    double? AverageActiveEnergyKcal);

public sealed record DashboardCycleMetrics(
    bool HasActiveCycle,
    bool IsPeriodDue,
    DateOnly? PredictedNextPeriodDate,
    DateOnly? PredictedOvulationDate,
    int CurrentDay,
    int PredictedCycleLength,
    int DaysUntilPeriod);

public sealed record DashboardSupportResult(
    string CycleInsightText,
    string HydrationTarget,
    string CalorieTarget,
    string SleepGoal,
    int ReadinessScore,
    string ReadinessLabel,
    int RecoveryScore,
    string RecoveryLabel);

public static class DashboardSupportRule
{
    public static DashboardSupportResult Build(
        DashboardCycleMetrics cycle,
        CyclePhase phase,
        float caloriesProgress,
        float? weight,
        int workoutDaysPerWeek,
        string workoutSessionProgressText,
        DashboardHealthMetrics? health = null,
        int? recordedReadinessScore = null,
        bool hasActiveInjury = false,
        int? localSteps = null,
        double? localAverageSleepHours = null)
    {
        var readinessScore = recordedReadinessScore is not null
            ? Math.Clamp(recordedReadinessScore.Value, 0, 100)
            : Math.Min(65,
                CalculateReadinessScore(phase, caloriesProgress, workoutDaysPerWeek, health, localSteps,
                    localAverageSleepHours));

        var recoveryScore = CalculateRecoveryScore(phase, caloriesProgress, workoutSessionProgressText, health,
            localAverageSleepHours);
        if (health is null && localAverageSleepHours is null)
            recoveryScore = Math.Min(recoveryScore, 70);

        var readinessCapped = hasActiveInjury && readinessScore > 85;
        var recoveryCapped = hasActiveInjury && recoveryScore > 85;

        if (hasActiveInjury)
        {
            readinessScore = Math.Min(readinessScore, 85);
            recoveryScore = Math.Min(recoveryScore, 85);
        }

        return new DashboardSupportResult(
            BuildCycleInsightText(cycle),
            BuildHydrationTarget(weight, health, localSteps),
            string.Empty,
            BuildSleepGoal(workoutDaysPerWeek, phase, health, localAverageSleepHours),
            readinessScore,
            readinessCapped ? "Limited by injury" : BuildReadinessLabel(readinessScore),
            recoveryScore,
            recoveryCapped ? "Recovery limited" : BuildRecoveryLabel(recoveryScore));
    }

    public static int CalculateReadinessScore(
        CyclePhase phase,
        float caloriesProgress,
        int workoutDaysPerWeek,
        DashboardHealthMetrics? health,
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

        var effectiveSteps = health?.HasMovementData == true
            ? health.AverageSteps
            : localSteps.GetValueOrDefault();

        var movementAdjustment = effectiveSteps switch
        {
            >= 11000 => 3,
            >= 8000 => 2,
            > 0 and < 4000 => -4,
            _ => 0
        };

        var effectiveSleep = health?.HasSleepData == true
            ? health.AverageSleepHours
            : localAverageSleepHours.GetValueOrDefault();

        var sleepAdjustment = effectiveSleep switch
        {
            >= 8 => 5,
            >= 7 => 2,
            > 0 and < 6 => -8,
            _ => 0
        };

        var heartAdjustment = health?.RestingHeartRate switch
        {
            > 100 => -6,
            > 90 => -3,
            _ => 0
        };

        var activeEnergyAdjustment = health?.AverageActiveEnergyKcal switch
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

    public static int CalculateRecoveryScore(
        CyclePhase phase,
        float caloriesProgress,
        string workoutSessionProgressText,
        DashboardHealthMetrics? health,
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
            workoutSessionProgressText,
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

        var effectiveSleep = health?.HasSleepData == true
            ? health.AverageSleepHours
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

    public static string BuildReadinessLabel(int score)
    {
        return score switch
        {
            >= 85 => "Strong training day",
            >= 70 => "Steady energy",
            >= 55 => "Keep it moderate",
            _ => "Prioritize gentle movement"
        };
    }

    public static string BuildRecoveryLabel(int score)
    {
        return score switch
        {
            >= 85 => "Well recovered",
            >= 70 => "Mostly recovered",
            >= 55 => "Needs care",
            _ => "Rest comes first"
        };
    }

    public static string BuildCycleInsightText(DashboardCycleMetrics cycle)
    {
        if (!cycle.HasActiveCycle)
            return "Start tracking to personalize cycle predictions";

        if (cycle.IsPeriodDue)
            return "Period is predicted around now";

        var nextPeriod = cycle.PredictedNextPeriodDate?.ToString("MMM d", CultureInfo.InvariantCulture);
        var ovulation = cycle.PredictedOvulationDate?.ToString("MMM d", CultureInfo.InvariantCulture);
        return nextPeriod is null
            ? $"Day {cycle.CurrentDay} of predicted {cycle.PredictedCycleLength}"
            : $"Next period in {cycle.DaysUntilPeriod}d · Ovulation {ovulation}";
    }

    public static string BuildHydrationTarget(float? weight, DashboardHealthMetrics? health, int? localSteps = null)
    {
        if (weight is not > 0f || !float.IsFinite(weight.Value))
            return "Unavailable";
        var liters = Math.Clamp(weight.Value * 0.035f, 2f, 3.8f);

        var effectiveSteps = health?.HasMovementData == true
            ? health.AverageSteps
            : localSteps.GetValueOrDefault();

        if (effectiveSteps >= 10000)
            liters = Math.Clamp(liters + 0.2f, 2f, 4.2f);

        return $"{liters:N1} L";
    }

    public static string BuildSleepGoal(
        int workoutDaysPerWeek,
        CyclePhase phase,
        DashboardHealthMetrics? health,
        double? localAverageSleepHours = null)
    {
        if (workoutDaysPerWeek < 0) return "Unavailable";
        var goal = workoutDaysPerWeek >= 4 ? 8d : 7.5d;
        if (phase is CyclePhase.Menstrual or CyclePhase.Luteal)
            goal = Math.Max(goal, 8d);

        var avgSleep = health is { HasSleepData: true }
            ? health.AverageSleepHours
            : localAverageSleepHours;

        return avgSleep is > 0d && double.IsFinite(avgSleep.Value)
            ? $"{goal:N1}h target · {avgSleep.Value:N1}h avg"
            : goal % 1d == 0d
                ? $"{goal:N0}h"
                : $"{goal:N1}h";
    }
}
