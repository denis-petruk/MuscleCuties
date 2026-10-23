using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Cycle.Overview;

namespace MuscleCuties.Infrastructure.PostgreSql.Cycle.Overview;

/// <summary>Reads owner-scoped cycle phase observations without normalizing unknown stored values away.</summary>
public sealed class PostgresCyclePhaseLogProjectionStore(PostgresDbContext db) : IServerCyclePhaseLogProjectionStore
{
    public async Task<IReadOnlyList<ServerCyclePhaseLog>> LoadAsync(Guid userId, DateOnly from, DateOnly through,
        CancellationToken cancellationToken) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync<IReadOnlyList<ServerCyclePhaseLog>>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);
            var rows = await db.CyclePhaseLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.LocalDate >= from && row.LocalDate <= through)
                .OrderBy(row => row.LocalDate).ThenBy(row => row.Id)
                .Select(row => new { row.Id, row.CycleLogId, row.Phase, row.FlowIntensity, row.LoggedAt, row.Note, row.CreatedAt })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<ServerCyclePhaseLog>)rows.Select(row => Map(row.Id, row.CycleLogId, row.Phase, row.FlowIntensity, row.LoggedAt, row.Note, row.CreatedAt)).ToArray();
        }).ConfigureAwait(false);

    private static ServerCyclePhaseLog Map(Guid id, Guid? cycleId, string phaseValue, string? flowValue,
        DateTime loggedAt, string? note, DateTime createdAt)
    {
        var isKnown = Enum.TryParse<CyclePhase>(phaseValue, true, out var phase) && Enum.IsDefined(phase);
        var knownFlow = Enum.TryParse<FlowIntensity>(flowValue, true, out var flow) && Enum.IsDefined(flow);
        return new ServerCyclePhaseLog(id, cycleId, phaseValue,
            isKnown ? $"cycle.phase.{phase.ToString().ToLowerInvariant()}" : "cycle.phase.unknown",
            isKnown, flowValue, knownFlow ? $"cycle.flow.{flow.ToString().ToLowerInvariant()}" :
                string.IsNullOrWhiteSpace(flowValue) ? null : "cycle.flow.unknown", loggedAt, note, createdAt);
    }
}
