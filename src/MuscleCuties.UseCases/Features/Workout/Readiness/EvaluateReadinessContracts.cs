using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Readiness.Contracts;

public sealed record EvaluateReadinessEvidenceRequest(
    ReadinessEvidence Evidence,
    CyclePhase Phase,
    ReproductiveContext? ReproductiveContext = null,
    CyclePhaseBaselines? Baselines = null);

public sealed record EvaluateReadinessEvidenceResponse(
    HealthResultStatus Status,
    int? Score,
    ReadinessTier? Tier,
    ObservationProvenance Provenance,
    double Coverage,
    IReadOnlyList<string> Reasons,
    string? ReviewedPolicyVersion);

public sealed record ComputeReadinessRequest(
    DailyInputs Inputs,
    CyclePhase Phase,
    ReproductiveContext? ReproductiveContext = null,
    CyclePhaseBaselines? Baselines = null);

public sealed record ComputeReadinessResponse(
    int Score,
    ReadinessTier Tier,
    IReadOnlyDictionary<string, int> Breakdown);

public interface IEvaluateReadinessUseCase
{
    EvaluateReadinessEvidenceResponse EvaluateEvidence(EvaluateReadinessEvidenceRequest request);
    ComputeReadinessResponse Compute(ComputeReadinessRequest request);
}
