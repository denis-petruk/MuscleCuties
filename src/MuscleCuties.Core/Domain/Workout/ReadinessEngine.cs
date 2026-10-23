using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

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

public sealed class ReadinessEngine : IReadinessEngine
{
    private const int DefaultZeroReadinessScore = 0;
    private readonly WorkoutPlanningConfig? _config;
    private readonly string? _reviewedPolicyVersion;

    public ReadinessEngine(WorkoutPlanningConfig? config, string? reviewedPolicyVersion = null)
    {
        _config = config;
        _reviewedPolicyVersion = reviewedPolicyVersion;
    }

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
                ["soreness"] = DefaultZeroReadinessScore,
                ["load"] = DefaultZeroReadinessScore,
                ["previousDayLoad"] = DefaultZeroReadinessScore
            });
    }
}
