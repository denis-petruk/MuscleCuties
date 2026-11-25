using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

/// <summary>A day whose phase she is correcting.</summary>
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

/// <summary>Whether the edit was applied, and the ordering issue when it was refused.</summary>
public sealed record CyclePhaseEditResult(CyclePhaseEditOutcome Outcome, CyclePhaseDto? SuggestedPhase = null)
{
    /// <summary>The result of a saved phase edit.</summary>
    public static CyclePhaseEditResult Saved { get; } = new(CyclePhaseEditOutcome.Saved);
    /// <summary>The result of a phase edit the server refused as invalid.</summary>
    public static CyclePhaseEditResult InvalidInput { get; } = new(CyclePhaseEditOutcome.InvalidInput);
    /// <summary>The result of a phase edit the server could not process.</summary>
    public static CyclePhaseEditResult Unavailable { get; } = new(CyclePhaseEditOutcome.Unavailable);

    /// <summary>The result of a phase edit refused because the date already holds another phase.</summary>
    public static CyclePhaseEditResult OrderConflict(CyclePhaseDto suggestedPhase) =>
        new(CyclePhaseEditOutcome.OrderConflict, suggestedPhase);
}

/// <summary>Corrects a day's phase.</summary>
public interface ICyclePhaseEditUseCase
{
    Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default);
}
