using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

public static class ReadinessEvidenceRule
{
    public static HealthResult<int> Evaluate(ReadinessEvidence? evidence, CyclePhase phase,
        WorkoutPlanningConfig? config, string? reviewedPolicyVersion,
        CyclePhaseBaselines? baselines = null,
        ReproductiveContext? reproductiveContext = null)
    {
        if (string.IsNullOrWhiteSpace(reviewedPolicyVersion))
            return HealthResult<int>.Unavailable("reviewPending");
        if (config?.Readiness is null)
            return HealthResult<int>.Unavailable("policyConfigUnavailable");

        var effectivePhase = reproductiveContext is not null && !reproductiveContext.AllowsCyclePhaseTrainingAdjustments
            ? CyclePhase.Unknown
            : phase;

        if (evidence is null || evidence.Date == default ||
            evidence.SleepHours?.IsAvailable != true ||
            evidence.Energy?.IsAvailable != true || evidence.Pain?.IsAvailable != true ||
            evidence.SleepThreeDayAverage?.IsAvailable != true ||
            evidence.StepsYesterday?.IsAvailable != true ||
            evidence.StepsSevenDayAverage?.IsAvailable != true ||
            effectivePhase == CyclePhase.Unknown || !Enum.IsDefined(effectivePhase))
            return HealthResult<int>.Unavailable("requiredInputUnavailable");
        if (evidence.StepsYesterday.IntervalStart is null || evidence.StepsYesterday.IntervalEnd is null ||
            evidence.StepsYesterday.IntervalEnd <= evidence.StepsYesterday.IntervalStart ||
            evidence.StepsSevenDayAverage.IntervalStart is null || evidence.StepsSevenDayAverage.IntervalEnd is null ||
            evidence.StepsSevenDayAverage.IntervalEnd <= evidence.StepsSevenDayAverage.IntervalStart)
            return HealthResult<int>.Unavailable("stepsCoverageUnavailable");

        if (evidence.SleepHours.Value is not { } sleep ||
            evidence.SleepThreeDayAverage.Value is not { } sleepAverage ||
            evidence.StepsYesterday.Value is not { } steps ||
            evidence.StepsSevenDayAverage.Value is not { } stepsAverage ||
            evidence.Energy.Value is not { } energy ||
            evidence.Pain.Value is not { } pain)
            return HealthResult<int>.Unavailable("requiredInputUnavailable");
        if (!double.IsFinite(sleep) || sleep is < 0 or > 24 ||
            !double.IsFinite(sleepAverage) || sleepAverage is < 0 or > 24 ||
            steps is < 0 or > 200_000 || stepsAverage is < 0 or > 200_000 ||
            energy is < 1 or > 5 || pain is < 0 or > 3)
            return HealthResult<int>.Unavailable("malformedInput");

        var inputs = DailyInputs.CreateSafe(evidence.Date, sleep, sleepAverage, steps,
            stepsAverage, energy, pain, false, null);
        var score = ReadinessRule.Compute(inputs, effectivePhase, config, baselines, reproductiveContext).Score;
        return new HealthResult<int>(HealthResultStatus.Available, score,
            ObservationProvenance.Calculated, 1, [], reviewedPolicyVersion);
    }
}
