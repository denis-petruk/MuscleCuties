using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

internal static class PhotoOwnerLock
{
    public static async Task AcquireAsync(PostgresDbContext context, Guid ownerId, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Photo owner locking requires an active tenant transaction.");
        // Every photo mutation locks owner before intent. Account purge takes FOR UPDATE
        // on the same owner, freezing inventory without an owner/intent lock inversion.
        await using var command = new NpgsqlCommand("SELECT id FROM app.users WHERE id = @owner FOR KEY SHARE",
            (NpgsqlConnection)context.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction());
        command.Parameters.AddWithValue("owner", ownerId);
        if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException("The photo owner is no longer available.");
    }
}
