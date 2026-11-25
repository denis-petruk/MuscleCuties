using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.UseCases.Features.Workout.Readiness.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Readiness;

/// <summary>Scores readiness from the day's signals, returning a neutral score when evidence is missing.</summary>
public sealed class EvaluateReadinessUseCase : IEvaluateReadinessUseCase
{
    private const int HighTierThreshold = 75;
    private const int ModerateTierThreshold = 50;
    private readonly IReadinessEngine _readinessEngine;

    /// <summary>Takes the readiness engine, which holds the scoring rules.</summary>
    public EvaluateReadinessUseCase(IReadinessEngine readinessEngine)
    {
        _readinessEngine = readinessEngine ?? throw new ArgumentNullException(nameof(readinessEngine));
    }

    /// <summary>Scores the evidence the client sent, used when the device has no stored history.</summary>
    public EvaluateReadinessEvidenceResponse EvaluateEvidence(EvaluateReadinessEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = _readinessEngine.Evaluate(
            request.Evidence,
            request.Phase,
            request.Baselines,
            request.ReproductiveContext);

        ReadinessTier? tier = null;
        if (result.Status == HealthResultStatus.Available && result.Value.HasValue)
        {
            var score = result.Value.Value;
            tier = score >= HighTierThreshold
                ? ReadinessTier.High
                : score >= ModerateTierThreshold
                    ? ReadinessTier.Moderate
                    : ReadinessTier.Low;
        }

        return new EvaluateReadinessEvidenceResponse(
            result.Status,
            result.Value,
            tier,
            result.Provenance,
            result.Coverage,
            result.Reasons,
            result.ReviewedPolicyVersion);
    }

    /// <summary>Computes the day's readiness from the stored signals, returning a neutral score when they are missing.</summary>
    public ComputeReadinessResponse Compute(ComputeReadinessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readiness = _readinessEngine.Compute(
            request.Inputs,
            request.Phase,
            request.Baselines,
            request.ReproductiveContext);

        return new ComputeReadinessResponse(
            readiness.Score,
            readiness.Tier,
            readiness.Breakdown);
    }
}
