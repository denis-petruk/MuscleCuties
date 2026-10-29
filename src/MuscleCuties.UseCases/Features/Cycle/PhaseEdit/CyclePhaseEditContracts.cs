using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public sealed record CyclePhaseEditRequest(DateTime Date, CyclePhaseDto Phase, string? Note);

/// <summary>
/// Persistence-free outcome of a manual phase edit. Presentation maps each value to its own copy so a
/// validation failure, an ordering conflict and an unreachable server never share one message.
/// </summary>
public enum CyclePhaseEditOutcome
{
    Saved,
    OrderConflict,
    InvalidInput,
    Unavailable
}

public sealed record CyclePhaseEditResult(CyclePhaseEditOutcome Outcome, CyclePhaseDto? SuggestedPhase = null)
{
    public static CyclePhaseEditResult Saved { get; } = new(CyclePhaseEditOutcome.Saved);
    public static CyclePhaseEditResult InvalidInput { get; } = new(CyclePhaseEditOutcome.InvalidInput);
    public static CyclePhaseEditResult Unavailable { get; } = new(CyclePhaseEditOutcome.Unavailable);

    public static CyclePhaseEditResult OrderConflict(CyclePhaseDto suggestedPhase) =>
        new(CyclePhaseEditOutcome.OrderConflict, suggestedPhase);
}

public interface ICyclePhaseEditUseCase
{
    Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default);
}
