using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;
using NpgsqlTypes;

// One bounded page per explicit invocation. The scheduler owns invocation and alerting.
const int PageSize = 5;
const int RequiredDiscoveryPoolSize = 2;
const string DiscoveryVariable = "MUSCLECUTIES_WORKOUT_DISCOVERY_CONNECTION";
const string TenantVariable = "MUSCLECUTIES_WORKOUT_TENANT_CONNECTION";
var discoveryConnection = Environment.GetEnvironmentVariable(DiscoveryVariable);
var tenantConnection = Environment.GetEnvironmentVariable(TenantVariable);
if (string.IsNullOrWhiteSpace(discoveryConnection) || string.IsNullOrWhiteSpace(tenantConnection))
    throw new InvalidOperationException("Both workout reconciliation connection settings are required.");

var discoverySettings = new NpgsqlConnectionStringBuilder(discoveryConnection);
var tenantSettings = new NpgsqlConnectionStringBuilder(tenantConnection);
if (!string.Equals(discoverySettings.Host, tenantSettings.Host, StringComparison.OrdinalIgnoreCase) ||
    discoverySettings.Port != tenantSettings.Port ||
    !string.Equals(discoverySettings.Database, tenantSettings.Database, StringComparison.Ordinal) ||
    discoverySettings.SslMode != SslMode.VerifyFull || tenantSettings.SslMode != SslMode.VerifyFull ||
    (discoverySettings.Pooling && discoverySettings.MaxPoolSize < RequiredDiscoveryPoolSize))
    throw new InvalidOperationException("Workout reconciliation requires the same database, verified TLS and a discovery pool of at least two connections.");
await using var discovery = NpgsqlDataSource.Create(discoveryConnection);
await using var tenant = NpgsqlDataSource.Create(tenantConnection);
await using var discoveryCheck = await discovery.OpenConnectionAsync();
await using var tenantCheck = await tenant.OpenConnectionAsync();
static async Task<(string User, bool CanReadAccounts, bool CanDiscover)> PermissionsAsync(NpgsqlConnection connection)
{
    await using var command = new NpgsqlCommand("""
        SELECT current_user, has_any_column_privilege(current_user, 'app.workout_account_planning_states', 'SELECT'),
               has_function_privilege(current_user, 'app.discover_pending_workout_owners(uuid,integer)', 'EXECUTE')
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    return (reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2));
}
var discoveryPermissions = await PermissionsAsync(discoveryCheck);
var tenantPermissions = await PermissionsAsync(tenantCheck);
if (discoveryPermissions.User == tenantPermissions.User || discoveryPermissions.CanReadAccounts ||
    !discoveryPermissions.CanDiscover || !tenantPermissions.CanReadAccounts || tenantPermissions.CanDiscover)
    throw new InvalidOperationException("Workout discovery and tenant credentials do not meet isolation requirements.");
await discoveryCheck.CloseAsync();
await tenantCheck.CloseAsync();

var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(tenant).Options;
var dispatcher = new PostgresWorkoutReconciliationDispatcher(discovery, new TenantContextFactory(options));
await using var cursorConnection = await discovery.OpenConnectionAsync();
await using var transaction = await cursorConnection.BeginTransactionAsync();
await using var readCursor = new NpgsqlCommand(
    "SELECT after_owner_id FROM app.workout_reconciliation_cursor WHERE singleton = true FOR UPDATE", cursorConnection, transaction);
var cursorValue = await readCursor.ExecuteScalarAsync();
if (cursorValue is null) throw new InvalidOperationException("Workout reconciliation cursor is missing.");
var cursor = cursorValue is DBNull ? (Guid?)null : (Guid)cursorValue;
var page = await dispatcher.ProcessPageAsync(cursor, PageSize);
await using var saveCursor = new NpgsqlCommand(
    "UPDATE app.workout_reconciliation_cursor SET after_owner_id = @cursor WHERE singleton = true", cursorConnection, transaction);
saveCursor.Parameters.AddWithValue("cursor", NpgsqlDbType.Uuid, (object?)page.NextCursor ?? DBNull.Value);
await saveCursor.ExecuteNonQueryAsync();
await transaction.CommitAsync();
// Failed accounts retain their pending flag and are retried on the next scan. Do not emit owner IDs.
if (page.FailedAccounts > 0)
{
    Console.Error.WriteLine("Workout reconciliation encountered account failures; pending accounts will be retried.");
    return 1;
}
return 0;

sealed class TenantContextFactory(DbContextOptions<PostgresDbContext> options) : IDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext() => new(options);
}
