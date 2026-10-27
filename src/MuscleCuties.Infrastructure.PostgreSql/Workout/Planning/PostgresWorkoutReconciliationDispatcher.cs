using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using Npgsql;
using NpgsqlTypes;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Processes a bounded page of pending accounts using separate discovery and tenant credentials.</summary>
public sealed class PostgresWorkoutReconciliationDispatcher(
    NpgsqlDataSource discoveryDataSource, IDbContextFactory<PostgresDbContext> tenantContexts)
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;

    // The host persists the returned cursor and restarts from null after the final page.
    public async Task<WorkoutReconciliationPage> ProcessPageAsync(Guid? afterOwnerId, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < MinimumPageSize or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        var owners = new List<Guid>();
        await using (var connection = await discoveryDataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var command = new NpgsqlCommand(
            "SELECT owner_id FROM app.discover_pending_workout_owners(@after, @limit)", connection))
        {
            command.Parameters.AddWithValue("after", NpgsqlDbType.Uuid, (object?)afterOwnerId ?? DBNull.Value);
            command.Parameters.AddWithValue("limit", pageSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) owners.Add(reader.GetGuid(0));
        }

        var failed = 0;
        foreach (var owner in owners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var context = await tenantContexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    context.ChangeTracker.Clear();
                    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    await PostgresTenantContext.SetForCurrentTransactionAsync(context, owner, cancellationToken).ConfigureAwait(false);
                    await WorkoutSessionAccountLock.AcquireAsync(context, owner, cancellationToken).ConfigureAwait(false);
                    await new PostgresWorkoutPlanReconciler(context).ReconcileAsync(owner, cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { failed++; }
        }
        return new WorkoutReconciliationPage(owners.Count, failed,
            owners.Count < pageSize ? null : owners[^1]);
    }
}

public sealed record WorkoutReconciliationPage(int VisitedAccounts, int FailedAccounts, Guid? NextCursor);
