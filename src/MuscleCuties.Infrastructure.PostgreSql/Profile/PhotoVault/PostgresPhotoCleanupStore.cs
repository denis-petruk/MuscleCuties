using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed record PhotoObjectCleanupWorkItem(
    Guid Id,
    Guid UserId,
    Guid PhotoId,
    string S3Key,
    string Reason,
    Guid LeaseToken,
    long LeaseGeneration,
    int AttemptCount);

public enum PhotoCleanupFailureCategory
{
    Transient,
    Permission,
    Configuration,
    Provider,
    Unknown
}

/// <summary>Durable database state only; S3 work is performed by a separately scoped worker.</summary>
public sealed class PostgresPhotoCleanupStore(PostgresDbContext dbContext)
{
    public async Task<IReadOnlyList<PhotoObjectCleanupWorkItem>> ClaimDueAsync(
        Guid userId,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A non-empty owner is required.", nameof(userId));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken);
            var token = Guid.NewGuid();
            var leaseUntil = nowUtc.Add(leaseDuration);
            var rows = new List<PhotoObjectCleanupWorkItem>();
            await using var command = new NpgsqlCommand("""
                WITH due AS (
                    SELECT id FROM app.photo_object_cleanups
                    WHERE user_id = @user_id AND not_before_utc <= @now_utc
                      AND (state IN ('Pending', 'Retryable') OR (state = 'Leased' AND lease_expires_at_utc <= @now_utc))
                    ORDER BY not_before_utc, created_at_utc
                    FOR UPDATE SKIP LOCKED
                    LIMIT @limit
                )
                UPDATE app.photo_object_cleanups c
                SET state = 'Leased', lease_token = @token,
                    lease_generation = c.lease_generation + 1,
                    lease_expires_at_utc = @lease_until,
                    attempt_count = c.attempt_count + 1,
                    last_attempt_at_utc = @now_utc
                FROM due
                WHERE c.id = due.id
                RETURNING c.id, c.user_id, c.photo_id, c.s3_key, c.reason, c.lease_token, c.lease_generation, c.attempt_count
                """, (NpgsqlConnection)dbContext.Database.GetDbConnection(), (NpgsqlTransaction)tx.GetDbTransaction());
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("now_utc", nowUtc);
            command.Parameters.AddWithValue("limit", limit);
            command.Parameters.AddWithValue("token", token);
            command.Parameters.AddWithValue("lease_until", leaseUntil);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add(new PhotoObjectCleanupWorkItem(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                    reader.GetString(3), reader.GetString(4), reader.GetGuid(5), reader.GetInt64(6), reader.GetInt32(7)));
            await reader.DisposeAsync();
            await tx.CommitAsync(cancellationToken);
            return (IReadOnlyList<PhotoObjectCleanupWorkItem>)rows;
        }).ConfigureAwait(false);
    }

    public async Task<bool> CompleteAsync(PhotoObjectCleanupWorkItem item, DateTime completedAtUtc, CancellationToken cancellationToken = default) =>
        await FencedUpdateAsync(item, """
            UPDATE app.photo_object_cleanups SET state = 'Completed', completed_at_utc = @completed_at,
                lease_token = NULL, lease_expires_at_utc = NULL, last_failure_category = NULL
            WHERE user_id = @user_id AND id = @id AND state = 'Leased'
              AND lease_token = @token AND lease_generation = @generation
            """, completedAtUtc, null, cancellationToken).ConfigureAwait(false);

    public async Task<bool> RetryAsync(
        PhotoObjectCleanupWorkItem item,
        DateTime attemptedAtUtc,
        DateTime retryAtUtc,
        PhotoCleanupFailureCategory category,
        CancellationToken cancellationToken = default)
    {
        if (retryAtUtc < attemptedAtUtc) throw new ArgumentOutOfRangeException(nameof(retryAtUtc));
        return await FencedUpdateAsync(item, """
            UPDATE app.photo_object_cleanups SET state = 'Retryable', not_before_utc = @retry_at,
                lease_token = NULL, lease_expires_at_utc = NULL, last_failure_category = @failure_category
            WHERE user_id = @user_id AND id = @id AND state = 'Leased'
              AND lease_token = @token AND lease_generation = @generation
            """, attemptedAtUtc, (retryAtUtc, category.ToString()), cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> FencedUpdateAsync(
        PhotoObjectCleanupWorkItem item,
        string sql,
        DateTime timestamp,
        (DateTime RetryAtUtc, string Category)? retry,
        CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, item.UserId, cancellationToken);
            await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)dbContext.Database.GetDbConnection(), (NpgsqlTransaction)tx.GetDbTransaction());
            command.Parameters.AddWithValue("user_id", item.UserId);
            command.Parameters.AddWithValue("id", item.Id);
            command.Parameters.AddWithValue("token", item.LeaseToken);
            command.Parameters.AddWithValue("generation", item.LeaseGeneration);
            if (retry is null) command.Parameters.AddWithValue("completed_at", timestamp);
            else
            {
                command.Parameters.AddWithValue("retry_at", retry.Value.RetryAtUtc);
                command.Parameters.AddWithValue("failure_category", retry.Value.Category);
            }
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return changed == 1;
        }).ConfigureAwait(false);
    }
}
