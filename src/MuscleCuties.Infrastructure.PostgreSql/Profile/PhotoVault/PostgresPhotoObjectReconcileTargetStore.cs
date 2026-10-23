using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>
/// Key-only reconciliation persistence. Construct with a dedicated connection authenticated as
/// musclecuties_photo_reconcile (a NOLOGIN role granted only this table's privileges); do not pass
/// the tenant/application connection. Provision that login and membership outside application
/// migrations. Targets are deliberately recurring: even an empty sweep only schedules another check.
/// </summary>
public sealed class PostgresPhotoObjectReconcileTargetStore(NpgsqlDataSource maintenanceDataSource)
{
    public async Task EnqueueAsync(string opaqueTargetKey, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        Validate(opaqueTargetKey, nowUtc);
        await using var connection = await maintenanceDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO app.photo_object_reconcile_targets (id, target_key, not_before_utc, created_at_utc)
            VALUES (@id, @key, @now, @now)
            ON CONFLICT (target_key) DO UPDATE SET not_before_utc = LEAST(app.photo_object_reconcile_targets.not_before_utc, EXCLUDED.not_before_utc)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("key", opaqueTargetKey);
        command.Parameters.AddWithValue("now", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PhotoObjectReconcileTarget>> ClaimDueAsync(
        DateTime nowUtc, TimeSpan leaseDuration, int limit,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentOutOfRangeException(nameof(nowUtc));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await maintenanceDataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        var leaseToken = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            WITH due AS (
                SELECT id FROM app.photo_object_reconcile_targets
                WHERE not_before_utc <= @now AND (lease_token IS NULL OR lease_expires_at_utc <= @now)
                ORDER BY not_before_utc, id FOR UPDATE SKIP LOCKED LIMIT @limit
            )
            UPDATE app.photo_object_reconcile_targets target
            SET lease_token = @token, lease_generation = target.lease_generation + 1, lease_expires_at_utc = @lease_until
            FROM due WHERE target.id = due.id
            RETURNING target.id, target.target_key, target.lease_generation
            """, connection, tx);
        command.Parameters.AddWithValue("now", nowUtc);
        command.Parameters.AddWithValue("token", leaseToken);
        command.Parameters.AddWithValue("lease_until", nowUtc.Add(leaseDuration));
        command.Parameters.AddWithValue("limit", limit);
        var result = new List<PhotoObjectReconcileTarget>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetString(1), leaseToken, reader.GetInt64(2)));
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>Fenced completion records only the next due time; there is no terminal state.</summary>
    public async Task<bool> ScheduleNextCheckAsync(PhotoObjectReconcileTarget target, DateTime nextCheckUtc, CancellationToken cancellationToken = default)
    {
        if (target.Id == Guid.Empty || target.LeaseToken == Guid.Empty) throw new ArgumentException("A claimed target is required.", nameof(target));
        if (nextCheckUtc.Kind != DateTimeKind.Utc) throw new ArgumentOutOfRangeException(nameof(nextCheckUtc));
        await using var connection = await maintenanceDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE app.photo_object_reconcile_targets SET not_before_utc = @next, lease_token = NULL, lease_expires_at_utc = NULL
            WHERE id = @id AND target_key = @key AND lease_token = @token AND lease_generation = @generation
            """, connection);
        command.Parameters.AddWithValue("next", nextCheckUtc);
        command.Parameters.AddWithValue("id", target.Id);
        command.Parameters.AddWithValue("key", target.TargetKey);
        command.Parameters.AddWithValue("token", target.LeaseToken);
        command.Parameters.AddWithValue("generation", target.LeaseGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void Validate(string key, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512 || key.StartsWith('/') || key.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("A valid opaque object key is required.", nameof(key));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentOutOfRangeException(nameof(nowUtc));
    }
}

public sealed record PhotoObjectReconcileTarget(Guid Id, string TargetKey, Guid LeaseToken, long LeaseGeneration);
