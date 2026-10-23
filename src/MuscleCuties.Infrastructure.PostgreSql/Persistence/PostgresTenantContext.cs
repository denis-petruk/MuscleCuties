using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>Sets the server-derived tenant identity for the active PostgreSQL transaction.</summary>
public static class PostgresTenantContext
{
    public const string UserIdSetting = "app.user_id";

    public static async Task SetForCurrentTransactionAsync(
        PostgresDbContext context,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (userId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(userId));

        var currentTransaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Tenant context requires an active database transaction.");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.user_id', @user_id, true)",
            connection,
            (NpgsqlTransaction)currentTransaction.GetDbTransaction());
        command.Parameters.AddWithValue("user_id", userId.ToString("D"));
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
