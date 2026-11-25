using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Readiness.Contracts;

/// <summary>Evidence to score readiness from, sent when the device has no stored history.</summary>
public sealed record EvaluateReadinessEvidenceRequest(
    ReadinessEvidence Evidence,
    CyclePhase Phase,
    ReproductiveContext? ReproductiveContext = null,
    CyclePhaseBaselines? Baselines = null);

/// <summary>The readiness the evidence implies.</summary>
public sealed record EvaluateReadinessEvidenceResponse(
    HealthResultStatus Status,
    int? Score,
    ReadinessTier? Tier,
    ObservationProvenance Provenance,
    double Coverage,
    IReadOnlyList<string> Reasons,
    string? ReviewedPolicyVersion);

/// <summary>A request to compute the day's readiness.</summary>
public sealed record ComputeReadinessRequest(
    DailyInputs Inputs,
    CyclePhase Phase,
    ReproductiveContext? ReproductiveContext = null,
    CyclePhaseBaselines? Baselines = null);

/// <summary>The computed readiness with the signals behind it.</summary>
public sealed record ComputeReadinessResponse(
    int Score,
    ReadinessTier Tier,
    IReadOnlyDictionary<string, int> Breakdown);

/// <summary>The readiness computation.</summary>
public interface IEvaluateReadinessUseCase
{
    EvaluateReadinessEvidenceResponse EvaluateEvidence(EvaluateReadinessEvidenceRequest request);
    ComputeReadinessResponse Compute(ComputeReadinessRequest request);
}
