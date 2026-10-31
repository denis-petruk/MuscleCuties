using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

public static class ReadinessRule
{
    private const int MinimumEnergy = 1;
    private const int MaximumEnergy = 5;
    private const int MinimumScore = 0;
    private const int MaximumScore = 100;
    private const int MinimumPhaseScore = -10;
    private const int MaximumPhaseScore = 20;
    private const int NeutralBaselineEnergy = 3;
    private const int NeutralBaselinePain = 1;
    private const int MaximumBaselinePain = 3;
    private const int PointsPerBaselineStep = 2;
    private const double PercentScale = 100.0;

    private const int VeryPoorSleepQualityAdjustment = -6;
    private const int PoorSleepQualityAdjustment = -3;
    private const int GoodSleepQualityAdjustment = 3;
    private const int VeryGoodSleepQualityAdjustment = 5;

    private const int MildSorenessAdjustment = -3;
    private const int ModerateSorenessAdjustment = -6;
    private const int SevereSorenessAdjustment = -10;

    private const double HighLoadThreshold = 80d;
    private const int HighLoadAdjustment = -6;
    private const double ModerateLoadThreshold = 50d;
    private const int ModerateLoadAdjustment = -4;
    private const double LightLoadThreshold = 20d;
    private const int LightLoadAdjustment = -2;

    private const int HighSetCount = 20;
    private const int HighSetCountAdjustment = -6;
    private const int ModerateSetCount = 15;
    private const int ModerateSetCountAdjustment = -4;
    private const int LowSetCount = 10;
    private const int LowSetCountAdjustment = -2;

    private const int ElevatedRestingHeartRate = 100;
    private const int ElevatedRestingHeartRateAdjustment = -6;
    private const int HighRestingHeartRate = 90;
    private const int HighRestingHeartRateAdjustment = -3;

    private const double HighActiveEnergyKcal = 900d;
    private const int HighActiveEnergyAdjustment = -3;
    private const double ElevatedActiveEnergyKcal = 650d;
    private const int ElevatedActiveEnergyAdjustment = -1;

    /// <summary>Missing check-in coverage cannot produce a positive readiness recommendation.</summary>
    public static Readiness ComputeForCheckInCoverage(
        DailyInputs? inputs, bool hasSleep, bool hasEnergy, bool hasPain,
        CyclePhase phase, WorkoutPlanningConfig? config,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        if (inputs is null || !hasSleep || !hasEnergy || !hasPain)
            return SafeDefault();
        return Compute(inputs, phase, config, baselines, reproductiveContext);
    }

    public static Readiness Compute(DailyInputs inputs, CyclePhase phase, WorkoutPlanningConfig? config,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        if (inputs is null || config?.Readiness is not { } rc)
            return SafeDefault();

        var effectivePhase = reproductiveContext is not null && !reproductiveContext.AllowsCyclePhaseTrainingAdjustments
            ? CyclePhase.Unknown
            : phase;

        var safeSleep = double.IsFinite(inputs.SleepHours) && inputs.SleepHours >= 0 ? inputs.SleepHours : 0;
        var safeSleep3d = double.IsFinite(inputs.Sleep3dAvg) && inputs.Sleep3dAvg >= 0 ? inputs.Sleep3dAvg : 0;
        var sleepScore = BandLookup(rc.SleepLastNight, safeSleep);
        var sleep3dScore = BandLookup(rc.Sleep3dAvg, safeSleep3d);
        var stepsScore = ComputeStepsScore(rc.StepsDeltaPct, inputs.StepsYesterday, inputs.Steps7dAvg);
        var boundedEnergy = Math.Clamp(inputs.Energy, MinimumEnergy, MaximumEnergy);
        var energyScore = (int)Math.Clamp((long)boundedEnergy * rc.EnergyMultiplier, MinimumScore, MaximumScore);
        var painScore = rc.Pain is { Length: > 0 }
            ? Math.Clamp(rc.Pain[Math.Clamp(inputs.Pain, 0, rc.Pain.Length - 1)], MinimumScore, MaximumScore)
            : 0;
        var phaseScore = ComputePhaseScore(rc, effectivePhase, baselines);
        var healthAdjustment = ComputeHealthAdjustment(inputs);
        var sleepQualityScore = ComputeSleepQualityAdjustment(inputs.SleepQuality);
        var sorenessScore = ComputeSorenessAdjustment(inputs.Soreness);
        var loadScore = ComputePreviousDayLoadAdjustment(inputs.PreviousDayLoad);

        var breakdown = new Dictionary<string, int>
        {
            ["sleep"] = sleepScore,
            ["sleep3d"] = sleep3dScore,
            ["steps"] = stepsScore,
            ["energy"] = energyScore,
            ["pain"] = painScore,
            ["phase"] = phaseScore,
            ["health"] = healthAdjustment,
            ["sleepQuality"] = sleepQualityScore,
            ["soreness"] = sorenessScore,
            ["load"] = loadScore,
            ["previousDayLoad"] = loadScore
        };

        var rawScore = (long)breakdown["sleep"] + breakdown["sleep3d"] + breakdown["steps"] +
                       breakdown["energy"] + breakdown["pain"] + breakdown["phase"] +
                       breakdown["health"] + breakdown["sleepQuality"] + breakdown["soreness"] +
                       breakdown["load"];
        var score = (int)Math.Clamp(rawScore, MinimumScore, MaximumScore);

        var tier = score >= rc.HighTier
            ? ReadinessTier.High
            : score >= rc.ModerateTier
                ? ReadinessTier.Moderate
                : ReadinessTier.Low;

        return new Readiness(score, tier, breakdown);
    }

    private static int ComputeSleepQualityAdjustment(SleepQuality? sleepQuality)
    {
        if (sleepQuality is null)
            return 0;

        return sleepQuality.Value switch
        {
            SleepQuality.VeryPoor => VeryPoorSleepQualityAdjustment,
            SleepQuality.Poor => PoorSleepQualityAdjustment,
            SleepQuality.Fair => 0,
            SleepQuality.Good => GoodSleepQualityAdjustment,
            SleepQuality.VeryGood => VeryGoodSleepQualityAdjustment,
            _ => 0
        };
    }

    private static int ComputeSorenessAdjustment(SorenessLevel? soreness)
    {
        if (soreness is null)
            return 0;

        return soreness.Value switch
        {
            SorenessLevel.None => 0,
            SorenessLevel.Mild => MildSorenessAdjustment,
            SorenessLevel.Moderate => ModerateSorenessAdjustment,
            SorenessLevel.Severe => SevereSorenessAdjustment,
            _ => 0
        };
    }

    private static int ComputePreviousDayLoadAdjustment(WorkoutLoad? load)
    {
        if (load is null || load.Kind is WorkoutLoadKind.Unavailable or WorkoutLoadKind.NoRecordedWorkout)
            return 0;

        if (load.Kind == WorkoutLoadKind.ReportedRest)
            return 0;

        if (load.LoadScalar.HasValue)
        {
            var scalar = load.LoadScalar.Value;
            if (!double.IsFinite(scalar) || scalar <= 0)
                return 0;

            return scalar switch
            {
                > HighLoadThreshold => HighLoadAdjustment,
                > ModerateLoadThreshold => ModerateLoadAdjustment,
                > LightLoadThreshold => LightLoadAdjustment,
                _ => 0
            };
        }

        if (load.SetsCompleted > 0)
            return load.SetsCompleted switch
            {
                >= HighSetCount => HighSetCountAdjustment,
                >= ModerateSetCount => ModerateSetCountAdjustment,
                >= LowSetCount => LowSetCountAdjustment,
                _ => 0
            };

        return 0;
    }

    private static int ComputeHealthAdjustment(DailyInputs inputs)
    {
        var rhrAdjustment = inputs.RestingHeartRate switch
        {
            > ElevatedRestingHeartRate => ElevatedRestingHeartRateAdjustment,
            > HighRestingHeartRate => HighRestingHeartRateAdjustment,
            _ => 0
        };

        var activeEnergyAdjustment = inputs.AverageActiveEnergyKcal is { } kcal && double.IsFinite(kcal) && kcal > 0
            ? kcal switch
            {
                > HighActiveEnergyKcal => HighActiveEnergyAdjustment,
                > ElevatedActiveEnergyKcal => ElevatedActiveEnergyAdjustment,
                _ => 0
            }
            : 0;

        return rhrAdjustment + activeEnergyAdjustment;
    }

    private static int ComputePhaseScore(ReadinessConfig rc, CyclePhase phase, CyclePhaseBaselines? baselines)
    {
        if (phase == CyclePhase.Unknown || !Enum.IsDefined(phase))
            return 0;

        var baseScore = rc.PhasePrior?.TryGetValue(phase.ToString(), out var prior) == true ? prior : 0;

        if (baselines is null)
            return Math.Clamp(baseScore, MinimumPhaseScore, MaximumPhaseScore);

        var baseline = baselines.ForPhase(phase);
        if (baseline is null)
            return baseScore;
        var energyAdjust = (Math.Clamp(baseline.Energy, MinimumEnergy, MaximumEnergy) - NeutralBaselineEnergy) *
                           PointsPerBaselineStep;
        var painAdjust = Math.Max(Math.Clamp(baseline.Pain, 0, MaximumBaselinePain) -
                                  NeutralBaselinePain, 0) * PointsPerBaselineStep;
        return (int)Math.Clamp((long)baseScore + energyAdjust - painAdjust, MinimumPhaseScore,
            MaximumPhaseScore);
    }

    private static int ComputeStepsScore(StepsDeltaConfig? cfg, int stepsYesterday, int steps7dAvg)
    {
        if (cfg is null || stepsYesterday <= 0 || steps7dAvg <= 0)
            return 0;

        var deltaPct = ((double)stepsYesterday - steps7dAvg) * PercentScale / steps7dAvg;

        if (deltaPct > cfg.SpikeThreshold)
            return Math.Clamp(cfg.Spike, MinimumScore, MaximumScore);

        if (deltaPct < cfg.DropThreshold)
            return Math.Clamp(cfg.Drop, MinimumScore, MaximumScore);

        return Math.Clamp(cfg.Normal, MinimumScore, MaximumScore);
    }

    private static int BandLookup(double[][]? bands, double value)
    {
        if (bands is null || !double.IsFinite(value) || value < 0)
            return 0;

        foreach (var band in bands)
            if (band is { Length: >= 2 } && double.IsFinite(band[0]) &&
                double.IsFinite(band[1]) && band[0] >= MinimumScore &&
                band[1] is >= MinimumScore and <= MaximumScore && value >= band[0])
                return (int)band[1];

        return 0;
    }

    private static Readiness SafeDefault()
    {
        return new Readiness(0, ReadinessTier.Low,
            new Dictionary<string, int>
            {
                ["sleep"] = 0,
                ["sleep3d"] = 0,
                ["steps"] = 0,
                ["energy"] = 0,
                ["pain"] = 0,
                ["phase"] = 0,
                ["health"] = 0,
                ["sleepQuality"] = 0,
                ["soreness"] = 0,
                ["load"] = 0,
                ["previousDayLoad"] = 0
            });
    }
}
