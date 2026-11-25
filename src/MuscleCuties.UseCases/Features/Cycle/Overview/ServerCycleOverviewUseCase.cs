using MuscleCuties.UseCases.Features.Cycle.History;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

/// <summary>The overview the server returns, already assembled from the rows.</summary>
public sealed record ServerCycleOverview(
    CyclePredictionProjection Prediction,
    CycleHistorySnapshot History,
    IReadOnlyList<ServerCyclePhaseLog> PhaseLogs,
    string PhaseCode,
    string PredictionSourceCode);

/// <summary>One phase log as the server returns it.</summary>
public sealed record ServerCyclePhaseLog(
    Guid Id,
    Guid? CycleId,
    string PhaseValue,
    string PhaseCode,
    bool IsKnownPhase,
    string? FlowIntensityValue,
    string? FlowIntensityCode,
    DateTime LoggedAt,
    string? Note,
    DateTime CreatedAt);

/// <summary>The projection the server reads for the overview.</summary>
public interface IServerCyclePhaseLogProjectionStore
{
    Task<IReadOnlyList<ServerCyclePhaseLog>> LoadAsync(Guid userId, DateOnly from, DateOnly through,
        CancellationToken cancellationToken);
}

/// <summary>Assembles the server's overview from the projected phase logs.</summary>
public sealed class ServerCycleOverviewUseCase(CycleHistoryUseCase history, IServerCyclePhaseLogProjectionStore phaseLogStore)
{
    /// <summary>Loads the server-side cycle overview for a user and range.</summary>
    public async Task<ServerCycleOverview> LoadAsync(Guid userId, DateOnly from, DateOnly through, DateOnly today,
        CancellationToken cancellationToken = default)
    {
        if (through < from) throw new ArgumentOutOfRangeException(nameof(through));
        var projection = await history.ProjectPredictionAsync(userId, today, cancellationToken).ConfigureAwait(false);
        var entries = await history.LoadAsync(userId, from, through, cancellationToken).ConfigureAwait(false);
        var phaseLogs = await phaseLogStore.LoadAsync(userId, from, through, cancellationToken).ConfigureAwait(false);
        var sourceCode = projection.PredictionSource switch
        {
            "recent cycles" => "cycle.prediction_source.recent_cycles",
            "cycle start history" => "cycle.prediction_source.cycle_start_history",
            "profile" => "cycle.prediction_source.profile",
            _ => "cycle.prediction_source.general_estimate"
        };
        return new(projection, entries, phaseLogs, $"cycle.phase.{projection.CurrentPhase.ToLowerInvariant()}", sourceCode);
    }
}
