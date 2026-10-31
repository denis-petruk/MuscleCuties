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
    private const int MinimumScore = 0;
    private const int MaximumScore = 100;
    private const int DefaultReadinessCap = 65;
    private const int DefaultRecoveryCap = 70;
    private const int HighScoreThreshold = 85;
    private const int SteadyScoreThreshold = 70;
    private const int ModerateScoreThreshold = 55;

    private const int ReadinessBaseline = 72;
    private const int RecoveryBaseline = 78;

    private const int MenstrualReadinessAdjustment = -10;
    private const int FollicularReadinessAdjustment = 8;
    private const int OvulatoryReadinessAdjustment = 12;
    private const int LutealReadinessAdjustment = -4;

    private const int MenstrualRecoveryAdjustment = -4;
    private const int FollicularRecoveryAdjustment = 5;
    private const int OvulatoryRecoveryAdjustment = 3;
    private const int LutealRecoveryAdjustment = -2;

    private const float BalancedNutritionMinimumProgress = 0.75f;
    private const float BalancedNutritionMaximumProgress = 1.15f;
    private const int BalancedNutritionReadinessAdjustment = 6;
    private const int BalancedNutritionRecoveryAdjustment = 4;
    private const float UnderEatingReadinessThreshold = 0.4f;
    private const int UnderEatingReadinessAdjustment = -8;
    private const float OverEatingReadinessThreshold = 1.35f;
    private const int OverEatingReadinessAdjustment = -4;
    private const float UnderEatingRecoveryThreshold = 0.45f;
    private const int UnderEatingRecoveryAdjustment = -7;

    private const int HighTrainingLoadDays = 5;
    private const int HighTrainingLoadAdjustment = -3;
    private const int ModerateTrainingLoadDays = 3;
    private const int ModerateTrainingLoadAdjustment = 2;

    private const int HighMovementSteps = 11000;
    private const int HighMovementAdjustment = 3;
    private const int ModerateMovementSteps = 8000;
    private const int ModerateMovementAdjustment = 2;
    private const int LowMovementSteps = 4000;
    private const int LowMovementAdjustment = -4;

    private const double SolidSleepHours = 8d;
    private const double AdequateSleepHours = 7d;
    private const double ShortSleepHours = 6d;
    private const int ReadinessSolidSleepAdjustment = 5;
    private const int ReadinessAdequateSleepAdjustment = 2;
    private const int ReadinessShortSleepAdjustment = -8;
    private const int RecoverySolidSleepAdjustment = 8;
    private const int RecoveryAdequateSleepAdjustment = 4;
    private const int RecoveryShortSleepAdjustment = -10;

    private const int ElevatedRestingHeartRate = 100;
    private const int ElevatedRestingHeartRateAdjustment = -6;
    private const int HighRestingHeartRate = 90;
    private const int HighRestingHeartRateAdjustment = -3;

    private const double HighActiveEnergyKcal = 900d;
    private const int HighActiveEnergyAdjustment = -3;
    private const double ElevatedActiveEnergyKcal = 650d;
    private const int ElevatedActiveEnergyAdjustment = -1;

    private const int CompletedWorkoutRecoveryAdjustment = -8;
    private const int IncompleteWorkoutRecoveryAdjustment = 4;

    private const float HydrationLitersPerKg = 0.035f;
    private const float MinimumHydrationLiters = 2f;
    private const float MaximumHydrationLiters = 3.8f;
    private const int HydrationStepBonusThreshold = 10000;
    private const float HydrationStepBonusLiters = 0.2f;
    private const float MaximumHydrationWithStepsLiters = 4.2f;

    private const int HighWorkoutFrequencyDays = 4;
    private const double HighFrequencySleepGoalHours = 8d;
    private const double StandardSleepGoalHours = 7.5d;
    private const double PhaseSleepGoalMinimumHours = 8d;

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
            ? Math.Clamp(recordedReadinessScore.Value, MinimumScore, MaximumScore)
            : Math.Min(DefaultReadinessCap,
                CalculateReadinessScore(phase, caloriesProgress, workoutDaysPerWeek, health, localSteps,
                    localAverageSleepHours));

        var recoveryScore = CalculateRecoveryScore(phase, caloriesProgress, workoutSessionProgressText, health,
            localAverageSleepHours);
        if (health is null && localAverageSleepHours is null)
            recoveryScore = Math.Min(recoveryScore, DefaultRecoveryCap);

        var readinessCapped = hasActiveInjury && readinessScore > HighScoreThreshold;
        var recoveryCapped = hasActiveInjury && recoveryScore > HighScoreThreshold;

        if (hasActiveInjury)
        {
            readinessScore = Math.Min(readinessScore, HighScoreThreshold);
            recoveryScore = Math.Min(recoveryScore, HighScoreThreshold);
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
            CyclePhase.Menstrual => MenstrualReadinessAdjustment,
            CyclePhase.Follicular => FollicularReadinessAdjustment,
            CyclePhase.Ovulatory => OvulatoryReadinessAdjustment,
            CyclePhase.Luteal => LutealReadinessAdjustment,
            _ => 0
        };

        var nutritionAdjustment = caloriesProgress switch
        {
            >= BalancedNutritionMinimumProgress and <= BalancedNutritionMaximumProgress =>
                BalancedNutritionReadinessAdjustment,
            < UnderEatingReadinessThreshold => UnderEatingReadinessAdjustment,
            > OverEatingReadinessThreshold => OverEatingReadinessAdjustment,
            _ => 0
        };

        var trainingAdjustment = workoutDaysPerWeek switch
        {
            >= HighTrainingLoadDays => HighTrainingLoadAdjustment,
            >= ModerateTrainingLoadDays => ModerateTrainingLoadAdjustment,
            _ => 0
        };

        var effectiveSteps = health?.HasMovementData == true
            ? health.AverageSteps
            : localSteps.GetValueOrDefault();

        var movementAdjustment = effectiveSteps switch
        {
            >= HighMovementSteps => HighMovementAdjustment,
            >= ModerateMovementSteps => ModerateMovementAdjustment,
            > 0 and < LowMovementSteps => LowMovementAdjustment,
            _ => 0
        };

        var effectiveSleep = health?.HasSleepData == true
            ? health.AverageSleepHours
            : localAverageSleepHours.GetValueOrDefault();

        var sleepAdjustment = effectiveSleep switch
        {
            >= SolidSleepHours => ReadinessSolidSleepAdjustment,
            >= AdequateSleepHours => ReadinessAdequateSleepAdjustment,
            > 0 and < ShortSleepHours => ReadinessShortSleepAdjustment,
            _ => 0
        };

        var heartAdjustment = health?.RestingHeartRate switch
        {
            > ElevatedRestingHeartRate => ElevatedRestingHeartRateAdjustment,
            > HighRestingHeartRate => HighRestingHeartRateAdjustment,
            _ => 0
        };

        var activeEnergyAdjustment = health?.AverageActiveEnergyKcal switch
        {
            > HighActiveEnergyKcal => HighActiveEnergyAdjustment,
            > ElevatedActiveEnergyKcal => ElevatedActiveEnergyAdjustment,
            _ => 0
        };

        return Math.Clamp(
            ReadinessBaseline + phaseAdjustment + nutritionAdjustment + trainingAdjustment + movementAdjustment +
            sleepAdjustment + heartAdjustment + activeEnergyAdjustment,
            MinimumScore,
            MaximumScore);
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
            CyclePhase.Menstrual => MenstrualRecoveryAdjustment,
            CyclePhase.Follicular => FollicularRecoveryAdjustment,
            CyclePhase.Ovulatory => OvulatoryRecoveryAdjustment,
            CyclePhase.Luteal => LutealRecoveryAdjustment,
            _ => 0
        };

        var completedWorkoutLoad = string.Equals(
            workoutSessionProgressText,
            "Completed",
            StringComparison.OrdinalIgnoreCase)
            ? CompletedWorkoutRecoveryAdjustment
            : IncompleteWorkoutRecoveryAdjustment;

        var nutritionAdjustment = caloriesProgress switch
        {
            >= BalancedNutritionMinimumProgress and <= BalancedNutritionMaximumProgress =>
                BalancedNutritionRecoveryAdjustment,
            < UnderEatingRecoveryThreshold => UnderEatingRecoveryAdjustment,
            _ => 0
        };

        var effectiveSleep = health?.HasSleepData == true
            ? health.AverageSleepHours
            : localAverageSleepHours.GetValueOrDefault();

        var sleepAdjustment = effectiveSleep switch
        {
            >= SolidSleepHours => RecoverySolidSleepAdjustment,
            >= AdequateSleepHours => RecoveryAdequateSleepAdjustment,
            > 0 and < ShortSleepHours => RecoveryShortSleepAdjustment,
            _ => 0
        };

        return Math.Clamp(
            RecoveryBaseline + phaseAdjustment + completedWorkoutLoad + nutritionAdjustment + sleepAdjustment,
            MinimumScore,
            MaximumScore);
    }

    public static string BuildReadinessLabel(int score)
    {
        return score switch
        {
            >= HighScoreThreshold => "Strong training day",
            >= SteadyScoreThreshold => "Steady energy",
            >= ModerateScoreThreshold => "Keep it moderate",
            _ => "Prioritize gentle movement"
        };
    }

    public static string BuildRecoveryLabel(int score)
    {
        return score switch
        {
            >= HighScoreThreshold => "Well recovered",
            >= SteadyScoreThreshold => "Mostly recovered",
            >= ModerateScoreThreshold => "Needs care",
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
        var liters = Math.Clamp(weight.Value * HydrationLitersPerKg, MinimumHydrationLiters,
            MaximumHydrationLiters);

        var effectiveSteps = health?.HasMovementData == true
            ? health.AverageSteps
            : localSteps.GetValueOrDefault();

        if (effectiveSteps >= HydrationStepBonusThreshold)
            liters = Math.Clamp(liters + HydrationStepBonusLiters, MinimumHydrationLiters,
                MaximumHydrationWithStepsLiters);

        return $"{liters:N1} L";
    }

    public static string BuildSleepGoal(
        int workoutDaysPerWeek,
        CyclePhase phase,
        DashboardHealthMetrics? health,
        double? localAverageSleepHours = null)
    {
        if (workoutDaysPerWeek < 0) return "Unavailable";
        var goal = workoutDaysPerWeek >= HighWorkoutFrequencyDays
            ? HighFrequencySleepGoalHours
            : StandardSleepGoalHours;
        if (phase is CyclePhase.Menstrual or CyclePhase.Luteal)
            goal = Math.Max(goal, PhaseSleepGoalMinimumHours);

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
