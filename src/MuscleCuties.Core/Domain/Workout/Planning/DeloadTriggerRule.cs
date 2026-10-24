namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Evaluates recovery and training performance records over consecutive weeks to detect
/// systemic fatigue and prescribe a clinical deload protocol (Spec Section 12).
/// Zero magic numbers; deterministic and conservative arithmetic.
/// </summary>
public static class DeloadTriggerRule
{
    public const int MinimumConsecutiveWeeksForDeload = 2;
    public const decimal MinDeloadSetReductionFraction = 0.30m;
    public const decimal MaxDeloadSetReductionFraction = 0.50m;
    public const decimal DefaultDeloadSetReductionFraction = 0.40m;
    public const int MinimumWorkingSetsPerMuscle = 1;
    public const int DefaultDeloadRirIncrease = 1;
    public const int MaxDeloadRirIncrease = 2;
    public const int MaxPrescribedTargetRir = 5;

    public const decimal PoorSleepQualityThreshold = 2.0m;
    public const decimal LowEnergyThreshold = 2.0m;
    public const decimal HighSorenessThreshold = 4.0m;

    public const int ZeroConsecutiveWeeks = 0;
    public const decimal ZeroReductionFraction = 0.0m;
    public const int ZeroRirIncrease = 0;

    public static DeloadPrescription Evaluate(IReadOnlyList<WeeklyPerformanceAndRecoveryRecord>? history)
    {
        if (history is null || history.Count == ZeroConsecutiveWeeks)
        {
            return new DeloadPrescription(
                IsDeloadTriggered: false,
                ConsecutiveTriggerWeeks: ZeroConsecutiveWeeks,
                SetReductionFraction: ZeroReductionFraction,
                TargetRirIncrease: ZeroRirIncrease,
                RemoveOptionalHardConditioning: false,
                RequiresPostDeloadReassessment: false,
                TriggerReason: "Insufficient history to evaluate deload criteria.");
        }

        var trailingConsecutiveCount = ZeroConsecutiveWeeks;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            var record = history[index];
            if (record is not null && record.CompoundUnderperformed && record.HasPoorRecovery)
            {
                trailingConsecutiveCount++;
            }
            else
            {
                break;
            }
        }

        if (trailingConsecutiveCount >= MinimumConsecutiveWeeksForDeload)
        {
            return new DeloadPrescription(
                IsDeloadTriggered: true,
                ConsecutiveTriggerWeeks: trailingConsecutiveCount,
                SetReductionFraction: DefaultDeloadSetReductionFraction,
                TargetRirIncrease: DefaultDeloadRirIncrease,
                RemoveOptionalHardConditioning: true,
                RequiresPostDeloadReassessment: true,
                TriggerReason: $"Triggered after {trailingConsecutiveCount} consecutive weeks of compound underperformance combined with poor recovery markers.");
        }

        return new DeloadPrescription(
            IsDeloadTriggered: false,
            ConsecutiveTriggerWeeks: trailingConsecutiveCount,
            SetReductionFraction: ZeroReductionFraction,
            TargetRirIncrease: ZeroRirIncrease,
            RemoveOptionalHardConditioning: false,
            RequiresPostDeloadReassessment: false,
            TriggerReason: "Deload threshold not met; normal progression criteria maintained.");
    }

    public static int CalculateDeloadSets(int originalSets, decimal reductionFraction = DefaultDeloadSetReductionFraction)
    {
        if (originalSets <= MinimumWorkingSetsPerMuscle)
        {
            return Math.Max(MinimumWorkingSetsPerMuscle, originalSets);
        }

        var clampedFraction = Math.Clamp(reductionFraction, MinDeloadSetReductionFraction, MaxDeloadSetReductionFraction);
        var reducedSets = (int)Math.Round(originalSets * (1.0m - clampedFraction), MidpointRounding.AwayFromZero);
        return Math.Max(MinimumWorkingSetsPerMuscle, reducedSets);
    }

    public static int CalculateDeloadRir(int currentRir, int rirIncrease = DefaultDeloadRirIncrease)
    {
        var clampedIncrease = Math.Clamp(rirIncrease, DefaultDeloadRirIncrease, MaxDeloadRirIncrease);
        var newRir = currentRir + clampedIncrease;
        return Math.Min(MaxPrescribedTargetRir, newRir);
    }
}

public sealed record WeeklyPerformanceAndRecoveryRecord(
    int WeekIndex,
    bool CompoundUnderperformed,
    decimal? AverageSleepQuality = null,
    decimal? AverageEnergy = null,
    decimal? AverageSoreness = null,
    bool? ExplicitPoorRecovery = null)
{
    public bool HasPoorRecovery
    {
        get
        {
            if (ExplicitPoorRecovery.HasValue)
            {
                return ExplicitPoorRecovery.Value;
            }

            var poorSleep = AverageSleepQuality.HasValue && AverageSleepQuality.Value <= DeloadTriggerRule.PoorSleepQualityThreshold;
            var lowEnergy = AverageEnergy.HasValue && AverageEnergy.Value <= DeloadTriggerRule.LowEnergyThreshold;
            var highSoreness = AverageSoreness.HasValue && AverageSoreness.Value >= DeloadTriggerRule.HighSorenessThreshold;

            return poorSleep || lowEnergy || highSoreness;
        }
    }
}

public sealed record DeloadPrescription(
    bool IsDeloadTriggered,
    int ConsecutiveTriggerWeeks,
    decimal SetReductionFraction,
    int TargetRirIncrease,
    bool RemoveOptionalHardConditioning,
    bool RequiresPostDeloadReassessment,
    string TriggerReason);
