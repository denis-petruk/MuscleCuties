using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseLog;

/// <summary>Authenticated owner and local calendar date identify the corrected phase log.</summary>
public sealed record SaveCyclePhaseLogInput(
    string? OwnerId,
    DateOnly LocalDate,
    CyclePhase Phase,
    FlowIntensity? FlowIntensity = null,
    string? Note = null);

public enum SaveCyclePhaseLogOutcomeKind
{
    Saved,
    InvalidInput,
    Conflict,
    PhaseOrderConflict
}

/// <summary>A persistence-free result; SuggestedPhase is populated for phase-order conflicts.</summary>
public sealed record SaveCyclePhaseLogOutput(
    SaveCyclePhaseLogOutcomeKind Outcome,
    CyclePhase? SuggestedPhase = null);

public interface ISaveCyclePhaseLogUseCase
{
    Task<SaveCyclePhaseLogOutput> ExecuteAsync(
        SaveCyclePhaseLogInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Performs owner/date upsert, legacy neighbor-order checks, and legacy note normalization
/// (trim, blank to null, cap at 1000 characters). Null flow preserves the existing day's flow.
/// </summary>
public interface ICyclePhaseLogStore
{
    Task<SaveCyclePhaseLogOutput> UpsertAsync(
        SaveCyclePhaseLogInput input,
        CancellationToken cancellationToken);
}
