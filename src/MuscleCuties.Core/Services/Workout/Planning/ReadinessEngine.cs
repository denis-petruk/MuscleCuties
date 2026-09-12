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
    private readonly WorkoutPlanningConfig _config;

    public ReadinessEngine(WorkoutPlanningConfig config)
    {
        _config = config;
    }

    public Readiness Compute(DailyInputs inputs, CyclePhase phase, CyclePhaseBaselines? baselines = null)
    {
        var rc = _config.Readiness;

        var sleepScore = BandLookup(rc.SleepLastNight, inputs.SleepHours);
        var sleep3dScore = BandLookup(rc.Sleep3dAvg, inputs.Sleep3dAvg);
        var stepsScore = ComputeStepsScore(rc.StepsDeltaPct, inputs.StepsYesterday, inputs.Steps7dAvg);
        var energyScore = (int)(Math.Clamp(inputs.Energy, 1, 5) * rc.EnergyMultiplier);
        var painScore = rc.Pain[Math.Clamp(inputs.Pain, 0, rc.Pain.Length - 1)];
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

        var rawScore = breakdown.Values.Sum();
        var score = Math.Clamp(rawScore, 0, 100);

        var tier = score >= rc.HighTier
            ? ReadinessTier.High
            : score >= rc.ModerateTier
                ? ReadinessTier.Moderate
                : ReadinessTier.Low;

        return new Readiness(score, tier, breakdown);
    }

    private static int ComputePhaseScore(ReadinessConfig rc, CyclePhase phase, CyclePhaseBaselines? baselines)
    {
        var baseScore = rc.PhasePrior.TryGetValue(phase.ToString(), out var prior) ? prior : 0;

        if (baselines is null)
            return baseScore;

        var baseline = baselines.ForPhase(phase);
        var energyAdjust = (baseline.Energy - 3) * 2;
        var painAdjust = Math.Max(baseline.Pain - 1, 0) * 2;
        return Math.Clamp(baseScore + energyAdjust - painAdjust, -10, 20);
    }

    private static int ComputeStepsScore(StepsDeltaConfig cfg, int stepsYesterday, int steps7dAvg)
    {
        if (steps7dAvg == 0)
            return cfg.Normal;

        var deltaPct = (stepsYesterday - steps7dAvg) * 100.0 / steps7dAvg;

        if (deltaPct > cfg.SpikeThreshold)
            return cfg.Spike;

        if (deltaPct < cfg.DropThreshold)
            return cfg.Drop;

        return cfg.Normal;
    }

    private static int BandLookup(double[][] bands, double value)
    {
        foreach (var band in bands)
        {
            if (value >= band[0])
                return (int)band[1];
        }

        return 0;
    }
}
