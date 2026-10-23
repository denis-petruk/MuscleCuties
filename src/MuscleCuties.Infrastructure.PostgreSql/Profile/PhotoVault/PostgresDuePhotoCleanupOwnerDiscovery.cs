using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class PostgresDuePhotoCleanupOwnerDiscovery(NpgsqlDataSource dataSource) : IDuePhotoCleanupOwnerDiscovery
{
    private const int MaximumOwnerLimit = 100;

    public async Task<IReadOnlyList<Guid>> DiscoverDueOwnersAsync(
        DateTime nowUtc,
        Guid? afterOwnerId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(nowUtc));
        if (limit is < 1 or > MaximumOwnerLimit) throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "SELECT owner_id FROM app.discover_due_photo_cleanup_owners(@now_utc, @after_owner_id, @limit)", connection);
        command.Parameters.AddWithValue("now_utc", nowUtc);
        command.Parameters.AddWithValue("after_owner_id", (object?)afterOwnerId ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);

        var owners = new List<Guid>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) owners.Add(reader.GetGuid(0));
        return owners;
    }
}
