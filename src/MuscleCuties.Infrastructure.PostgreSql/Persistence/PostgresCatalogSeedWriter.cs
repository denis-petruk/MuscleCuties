using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>Upserts one catalog row by its reviewed immutable seed code without changing its database ID.</summary>
public static class PostgresCatalogSeedWriter
{
    private const int MinimumFdcIdentifier = 1;
    private const string FdcSeedCodePrefix = "fdc:";

    public static string CreateFdcSeedCode(int fdcId)
    {
        if (fdcId < MinimumFdcIdentifier)
            throw new ArgumentOutOfRangeException(nameof(fdcId), "FDC identifiers must be positive.");

        return $"{FdcSeedCodePrefix}{fdcId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static async Task<TEntity> UpsertBySeedCodeAsync<TEntity>(
        PostgresDbContext context,
        DbSet<TEntity> rows,
        string seedCode,
        Func<TEntity> create,
        Action<TEntity> updateMutableFields,
        CancellationToken cancellationToken = default)
        where TEntity : class, ICatalogSeedRecord
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedCode);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(updateMutableFields);

        var existing = await rows.SingleOrDefaultAsync(
            row => row.SeedCode == seedCode,
            cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var created = create();
            if (!string.Equals(created.SeedCode, seedCode, StringComparison.Ordinal))
                throw new InvalidOperationException("The new catalog row must carry the requested immutable seed code.");

            rows.Add(created);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return created;
        }

        var originalId = existing.Id;
        updateMutableFields(existing);
        if (existing.Id != originalId
            || !string.Equals(existing.SeedCode, seedCode, StringComparison.Ordinal))
            throw new InvalidOperationException("Catalog seed updates cannot change the database ID or immutable seed code.");

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }
}
