using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IReadinessEngine
{
    Readiness Compute(DailyInputs inputs, CyclePhase phase, CyclePhaseBaselines? baselines = null);
}

public sealed class ReadinessEngine : IReadinessEngine
{
    private readonly WorkoutPlanningConfig? _config;

    public ReadinessEngine(WorkoutPlanningConfig? config)
    {
        _config = config;
    }

    public Readiness Compute(DailyInputs inputs, CyclePhase phase, CyclePhaseBaselines? baselines = null)
    {
        if (inputs is null || _config?.Readiness is not { } rc)
            return SafeDefault();

        var sleepScore = BandLookup(rc.SleepLastNight, inputs.SleepHours);
        var sleep3dScore = BandLookup(rc.Sleep3dAvg, inputs.Sleep3dAvg);
        var stepsScore = ComputeStepsScore(rc.StepsDeltaPct, inputs.StepsYesterday, inputs.Steps7dAvg);
        var energyScore = (int)Math.Clamp((long)Math.Clamp(inputs.Energy, 1, 5) * rc.EnergyMultiplier, 0L, 100L);
        var painScore = rc.Pain is { Length: > 0 }
            ? Math.Clamp(rc.Pain[Math.Clamp(inputs.Pain, 0, rc.Pain.Length - 1)], 0, 100)
            : 0;
        var phaseScore = ComputePhaseScore(rc, phase, baselines);

        var breakdown = new Dictionary<string, int>
        {
            ["sleep"] = sleepScore,
            ["sleep3d"] = sleep3dScore,
            ["steps"] = stepsScore,
            ["energy"] = energyScore,
            ["pain"] = painScore,
            ["phase"] = phaseScore
        };

        var rawScore = breakdown.Values.Sum(value => (long)value);
        var score = (int)Math.Clamp(rawScore, 0L, 100L);

        var tier = score >= rc.HighTier
            ? ReadinessTier.High
            : score >= rc.ModerateTier
                ? ReadinessTier.Moderate
                : ReadinessTier.Low;

        return new Readiness(score, tier, breakdown);
    }

    private static int ComputePhaseScore(ReadinessConfig rc, CyclePhase phase, CyclePhaseBaselines? baselines)
    {
        var baseScore = rc.PhasePrior?.TryGetValue(phase.ToString(), out var prior) == true ? prior : 0;

        if (baselines is null)
            return Math.Clamp(baseScore, -10, 20);

        var baseline = baselines.ForPhase(phase);
        if (baseline is null)
            return baseScore;
        var energyAdjust = (Math.Clamp(baseline.Energy, 1, 5) - 3) * 2;
        var painAdjust = Math.Max(Math.Clamp(baseline.Pain, 0, 3) - 1, 0) * 2;
        return (int)Math.Clamp((long)baseScore + energyAdjust - painAdjust, -10L, 20L);
    }

    private static int ComputeStepsScore(StepsDeltaConfig? cfg, int stepsYesterday, int steps7dAvg)
    {
        if (cfg is null || stepsYesterday <= 0 || steps7dAvg <= 0)
            return 0;

        var deltaPct = ((double)stepsYesterday - steps7dAvg) * 100.0 / steps7dAvg;

        if (deltaPct > cfg.SpikeThreshold)
            return Math.Clamp(cfg.Spike, 0, 100);

        if (deltaPct < cfg.DropThreshold)
            return Math.Clamp(cfg.Drop, 0, 100);

        return Math.Clamp(cfg.Normal, 0, 100);
    }

    private static int BandLookup(double[][]? bands, double value)
    {
        if (bands is null || !double.IsFinite(value) || value < 0)
            return 0;

        foreach (var band in bands)
        {
            if (band is { Length: >= 2 } && double.IsFinite(band[0]) &&
                double.IsFinite(band[1]) && band[0] >= 0 && band[1] is >= 0 and <= 100 &&
                value >= band[0])
                return (int)band[1];
        }

        return 0;
    }

    private static Readiness SafeDefault() => new(0, ReadinessTier.Low,
        new Dictionary<string, int>
        {
            ["sleep"] = 0,
            ["sleep3d"] = 0,
            ["steps"] = 0,
            ["energy"] = 0,
            ["pain"] = 0,
            ["phase"] = 0
        });
}
