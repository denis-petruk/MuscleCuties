using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IReadinessEngine : MuscleCuties.Core.Domain.Workout.IReadinessEngine
{
    Readiness ComputeFromHistory(
        IReadOnlyList<DailyReadinessLog>? history,
        DateOnly targetDate,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        DailyInputs? todayInputs = null);
}

public sealed class DailyReadinessEngine : IReadinessEngine
{
    private readonly MuscleCuties.Core.Domain.Workout.ReadinessEngine _inner;

    public DailyReadinessEngine(WorkoutPlanningConfig? config, string? reviewedPolicyVersion = null)
    {
        _inner = new MuscleCuties.Core.Domain.Workout.ReadinessEngine(config, reviewedPolicyVersion);
    }

    public HealthResult<int> Evaluate(
        ReadinessEvidence evidence,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        return _inner.Evaluate(evidence, phase, baselines, reproductiveContext);
    }

    public Readiness Compute(
        DailyInputs inputs,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        return _inner.Compute(inputs, phase, baselines, reproductiveContext);
    }

    public Readiness ComputeFromHistory(
        IReadOnlyList<DailyReadinessLog>? history,
        DateOnly targetDate,
        CyclePhase phase,
        CyclePhaseBaselines? baselines = null,
        DailyInputs? todayInputs = null)
    {
        var observations = history?.Where(log => log is not null)
            .Select(log => new ReadinessHistoryObservation(log.Date, log.SleepHours,
                log.StepsYesterday, log.Energy, log.Pain, log.Bloating,
                log.WeightKg, log.SleepQuality, log.Soreness, log.PreviousDayLoad))
            .ToArray();
        return _inner.ComputeFromHistory(observations, targetDate, phase, baselines, todayInputs, null);
    }

    Readiness MuscleCuties.Core.Domain.Workout.IReadinessEngine.ComputeFromHistory(
        IReadOnlyList<ReadinessHistoryObservation>? history,
        DateOnly targetDate,
        CyclePhase phase,
        CyclePhaseBaselines? baselines,
        DailyInputs? todayInputs,
        ReproductiveContext? reproductiveContext)
    {
        return _inner.ComputeFromHistory(history, targetDate, phase, baselines, todayInputs, reproductiveContext);
    }
}
