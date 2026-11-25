
namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Persists the non-sensitive owner continuation for explicitly invoked cleanup runs.</summary>
public sealed class PostgresPhotoCleanupDispatchCursor(NpgsqlDataSource dataSource)
{
    private const long DispatchAdvisoryLockKey = 70620261018025412;

    /// <summary>Takes the dispatch lease, so only one dispatcher walks the accounts at a time.</summary>
    public async Task<DispatchCursorLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lock_key)", connection);
            command.Parameters.AddWithValue("lock_key", DispatchAdvisoryLockKey);
            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not true)
                throw new InvalidOperationException("Another photo cleanup dispatch is already running.");
            return new DispatchCursorLease(dataSource, connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The lease and cursor a dispatch pass holds; disposing it releases both.</summary>
    public sealed class DispatchCursorLease(NpgsqlDataSource dataSource, NpgsqlConnection lockConnection) : IAsyncDisposable
    {
        /// <summary>The owner the next pass starts after, or nothing when it should start from the beginning.</summary>
        public async Task<Guid?> LoadAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT after_owner_id FROM app.photo_cleanup_dispatch_state WHERE state_id = TRUE", connection);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is Guid ownerId ? ownerId : null;
        }

        /// <summary>Stores where the pass reached, so the next one resumes without rescanning.</summary>
        public async Task SaveAsync(Guid? afterOwnerId, CancellationToken cancellationToken = default)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = new NpgsqlCommand(
                "UPDATE app.photo_cleanup_dispatch_state SET after_owner_id = @after_owner_id WHERE state_id = TRUE", connection);
            command.Parameters.AddWithValue("after_owner_id", (object?)afterOwnerId ?? DBNull.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("Photo cleanup dispatch cursor state is not initialized.");
        }

        /// <summary>Releases the advisory lock connection.</summary>
        public ValueTask DisposeAsync() => lockConnection.DisposeAsync();
    }
}
