using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle;

/// <summary>A phase edit that would put phases out of order, carrying the issue for the client.</summary>
public sealed class CyclePhaseOrderIssue(string message, CyclePhaseDto suggestedPhase)
    : InvalidOperationException(message)
{
    /// <summary>Phase the phase log suggests instead of the rejected one.</summary>
    public CyclePhaseDto SuggestedPhase { get; } = suggestedPhase;
}
