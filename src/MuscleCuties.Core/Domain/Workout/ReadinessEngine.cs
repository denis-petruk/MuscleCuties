using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Scores readiness from the day's inputs, so the plan reduces load only when the score says so.</summary>
public interface IReadinessEngine
{
    HealthResult<int> Evaluate(
        ReadinessEvidence evidence,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null);

    Readiness Compute(
        DailyInputs inputs,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null);

    Readiness ComputeFromHistory(
        IReadOnlyList<ReadinessHistoryObservation>? history,
        DateOnly targetDate,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        DailyInputs? todayInputs = null,
        ReproductiveContext? reproductiveContext = null);
}

/// <summary>The scoring engine: applies the configured weights and falls back to a neutral score when inputs are missing.</summary>
public sealed class ReadinessEngine : IReadinessEngine
{
    private const int DefaultZeroReadinessScore = 0;
    private readonly WorkoutPlanningConfig? _config;
    private readonly string? _reviewedPolicyVersion;

    /// <summary>Takes the planning config and the reviewed policy version the score is stamped with.</summary>
    public ReadinessEngine(WorkoutPlanningConfig? config, string? reviewedPolicyVersion = null)
    {
        _config = config;
        _reviewedPolicyVersion = reviewedPolicyVersion;
    }

    /// <summary>Scores the readiness evidence, or reports what is missing.</summary>
    public HealthResult<int> Evaluate(
        ReadinessEvidence evidence,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        return ReadinessEvidenceRule.Evaluate(
            evidence,
            phase,
            _config,
            _reviewedPolicyVersion,
            baselines,
            reproductiveContext);
    }

    /// <summary>Computes readiness from the day's inputs.</summary>
    public Readiness Compute(
        DailyInputs inputs,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        return ReadinessRule.Compute(
            inputs,
            phase,
            _config,
            baselines,
            reproductiveContext);
    }

    /// <summary>Computes readiness from the recorded daily inputs.</summary>
    public Readiness ComputeFromHistory(
        IReadOnlyList<ReadinessHistoryObservation>? history,
        DateOnly targetDate,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        DailyInputs? todayInputs = null,
        ReproductiveContext? reproductiveContext = null)
    {
        var inputs = ReadinessHistoryRule.Build(history, targetDate, todayInputs);
        return inputs is null ? SafeDefault() : Compute(inputs, phase, baselines, reproductiveContext);
    }

    /// <summary>The neutral readiness used when a signal is missing, so an unknown day never blocks training.</summary>
    private static Readiness SafeDefault()
    {
        return new Readiness(DefaultZeroReadinessScore, ReadinessTier.Low,
            new Dictionary<string, int>
            {
                ["sleep"] = DefaultZeroReadinessScore,
                ["sleep3d"] = DefaultZeroReadinessScore,
                ["steps"] = DefaultZeroReadinessScore,
                ["energy"] = DefaultZeroReadinessScore,
                ["pain"] = DefaultZeroReadinessScore,
                ["phase"] = DefaultZeroReadinessScore,
                ["health"] = DefaultZeroReadinessScore,
                ["sleepQuality"] = DefaultZeroReadinessScore,
                ["load"] = DefaultZeroReadinessScore,
                ["previousDayLoad"] = DefaultZeroReadinessScore
            });
    }
}
