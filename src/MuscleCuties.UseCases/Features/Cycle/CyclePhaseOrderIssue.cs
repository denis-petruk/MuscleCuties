using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle;

public sealed class CyclePhaseOrderIssue(string message, CyclePhaseDto suggestedPhase)
    : InvalidOperationException(message)
{
    public CyclePhaseDto SuggestedPhase { get; } = suggestedPhase;
}
