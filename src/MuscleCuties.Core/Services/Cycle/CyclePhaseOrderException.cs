using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle;

public sealed class CyclePhaseOrderException : InvalidOperationException
{
    public CyclePhaseOrderException(string message, CyclePhase suggestedPhase)
        : base(message)
    {
        SuggestedPhase = suggestedPhase;
    }

    public CyclePhase SuggestedPhase { get; }
}
