using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;

// Database advisory locks serialize reservations across every API instance sharing PostgreSQL.
internal sealed class PostgresPasswordLoginLimiter(
    PostgresDbContext dbContext,
    TimeProvider timeProvider,
    byte[] signingKey)
{
    private const int AccountWindowMinutes = 15;
    private const int SourceWindowMinutes = 5;
    private const int MaximumAccountAttempts = 8;
    private const int MaximumSourceAttempts = 20;
    private const int AttemptRetentionDays = 2;
    private const string AccountScope = "password-login-account-v1";
    private const string CanonicalAccountScope = "password-login-user-v1";
    private const string SourceScope = "password-login-source-v1";
    private const string UnknownSource = "unknown-source";

    public async Task<bool> ReserveAsync(string normalizedIdentifier, string? sourceAddress, CancellationToken cancellationToken)
    {
        var accountKey = ScopeKey(AccountScope, normalizedIdentifier);
        var sourceKey = ScopeKey(SourceScope, NormalizeSource(sourceAddress));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ReserveTransactionalAsync(accountKey, sourceKey, now, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<bool> ReserveTransactionalAsync(
        string accountKey,
        string sourceKey,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockScopesAsync(connection, postgresTransaction, accountKey, sourceKey, cancellationToken).ConfigureAwait(false);

        var accountAttempts = await CountRecentAsync(connection, postgresTransaction, accountKey,
            now.AddMinutes(-AccountWindowMinutes), cancellationToken).ConfigureAwait(false);
        var sourceAttempts = await CountRecentAsync(connection, postgresTransaction, sourceKey,
            now.AddMinutes(-SourceWindowMinutes), cancellationToken).ConfigureAwait(false);
        if (accountAttempts >= MaximumAccountAttempts || sourceAttempts >= MaximumSourceAttempts)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await DeleteExpiredAsync(connection, postgresTransaction, now.AddDays(-AttemptRetentionDays), cancellationToken)
            .ConfigureAwait(false);
        await InsertAttemptAsync(connection, postgresTransaction, accountKey, now, cancellationToken).ConfigureAwait(false);
        await InsertAttemptAsync(connection, postgresTransaction, sourceKey, now, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ReserveCanonicalAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var accountKey = ScopeKey(CanonicalAccountScope, userId.ToString("D"));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ReserveCanonicalTransactionalAsync(accountKey, now, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<bool> ReserveCanonicalTransactionalAsync(
        string accountKey,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockScopeAsync(connection, postgresTransaction, accountKey, cancellationToken).ConfigureAwait(false);
        var attempts = await CountRecentAsync(connection, postgresTransaction, accountKey,
            now.AddMinutes(-AccountWindowMinutes), cancellationToken).ConfigureAwait(false);
        if (attempts >= MaximumAccountAttempts)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await InsertAttemptAsync(connection, postgresTransaction, accountKey, now, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ClearAccountAfterSuccessAsync(UserRecord user, string normalizedIdentifier, CancellationToken cancellationToken)
    {
        var scopeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            ScopeKey(CanonicalAccountScope, user.Id.ToString("D")),
            ScopeKey(AccountScope, normalizedIdentifier),
            ScopeKey(AccountScope, user.NormalizedEmail)
        };
        if (!string.IsNullOrWhiteSpace(user.Username))
            scopeKeys.Add(ScopeKey(AccountScope, user.Username.Trim().ToLowerInvariant()));

        await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ClearAccountTransactionalAsync(scopeKeys, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task ClearAccountTransactionalAsync(
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        foreach (var lockKey in scopeKeys.Select(AdvisoryLockKey).Distinct().Order())
            await LockScopeAsync(connection, postgresTransaction, lockKey, cancellationToken).ConfigureAwait(false);
        foreach (var scopeKey in scopeKeys)
        {
            await using var command = new NpgsqlCommand(
                "DELETE FROM app.password_login_attempts WHERE scope_key = @scope_key", connection, postgresTransaction);
            command.Parameters.AddWithValue("scope_key", scopeKey);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private string ScopeKey(string scope, string value) => Convert.ToHexString(
        HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes($"{scope}:{value}")));

    private static string NormalizeSource(string? sourceAddress)
    {
        if (!IPAddress.TryParse(sourceAddress, out var address))
            return UnknownSource;
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static async Task LockScopesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string accountKey, string sourceKey, CancellationToken cancellationToken)
    {
        var accountLock = AdvisoryLockKey(accountKey);
        var sourceLock = AdvisoryLockKey(sourceKey);
        await LockScopeAsync(connection, transaction, accountLock < sourceLock ? accountLock : sourceLock, cancellationToken)
            .ConfigureAwait(false);
        if (accountLock != sourceLock)
            await LockScopeAsync(connection, transaction, accountLock < sourceLock ? sourceLock : accountLock, cancellationToken)
                .ConfigureAwait(false);
    }

    private static Task LockScopeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string scopeKey, CancellationToken cancellationToken) =>
        LockScopeAsync(connection, transaction, AdvisoryLockKey(scopeKey), cancellationToken);

    private static async Task LockScopeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long lockKey, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lock_key)", connection, transaction);
        command.Parameters.AddWithValue("lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static long AdvisoryLockKey(string scopeKey) =>
        BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(scopeKey)[..sizeof(long)]);

    private static async Task<long> CountRecentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string scopeKey,
        DateTime sinceUtc, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM app.password_login_attempts WHERE scope_key = @scope_key AND attempted_at_utc > @since_utc",
            connection, transaction);
        command.Parameters.AddWithValue("scope_key", scopeKey);
        command.Parameters.AddWithValue("since_utc", sinceUtc);
        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(count, CultureInfo.InvariantCulture);
    }

    private static async Task InsertAttemptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string scopeKey,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO app.password_login_attempts(id, scope_key, attempted_at_utc) VALUES (@id, @scope_key, @attempted_at_utc)",
            connection, transaction);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("scope_key", scopeKey);
        command.Parameters.AddWithValue("attempted_at_utc", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteExpiredAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "DELETE FROM app.password_login_attempts WHERE attempted_at_utc < @cutoff_utc",
            connection, transaction);
        command.Parameters.AddWithValue("cutoff_utc", cutoffUtc);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
