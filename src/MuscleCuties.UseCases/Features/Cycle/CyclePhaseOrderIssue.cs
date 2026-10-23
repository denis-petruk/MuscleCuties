using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle;

public sealed class CyclePhaseOrderIssue(string message, CyclePhase suggestedPhase)
    : InvalidOperationException(message)
{
    public CyclePhase SuggestedPhase { get; } = suggestedPhase;
}
