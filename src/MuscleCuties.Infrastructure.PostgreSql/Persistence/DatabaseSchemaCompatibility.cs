namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>
/// Bounded, read-only deployment-order guard. The database migration ledger is compared with the
/// migrations compiled into the running assembly, so a deployment that replaced Lambda code ahead of
/// pending schema changes is reported instead of appearing healthy. No DDL is applied and no schema
/// object names or SQL text leave this type.
/// </summary>
public static class DatabaseSchemaCompatibility
{
    private const int NoDrift = 0;
    private const string CompatibleDescription = "Database migration ledger matches the deployed assembly.";
    private const string PendingMigrationsDescription =
        "Database schema is behind the deployed assembly; apply the pending migrations before serving traffic.";
    private const string UnknownAppliedMigrationsDescription =
        "Database migration ledger contains migrations unknown to the deployed assembly; deploy matching code before serving traffic.";
    private const string DivergentDescription =
        "Database migration ledger and the deployed assembly both contain unshared migrations; reconcile the deployment before serving traffic.";

    public static DatabaseSchemaCompatibilityResult Evaluate(
        IEnumerable<string> appliedMigrations,
        IEnumerable<string> knownMigrations)
    {
        ArgumentNullException.ThrowIfNull(appliedMigrations);
        ArgumentNullException.ThrowIfNull(knownMigrations);

        var applied = new HashSet<string>(appliedMigrations, StringComparer.Ordinal);
        var known = new HashSet<string>(knownMigrations, StringComparer.Ordinal);
        var pendingCount = known.Count(migration => !applied.Contains(migration));
        var unknownAppliedCount = applied.Count(migration => !known.Contains(migration));

        if (pendingCount == NoDrift && unknownAppliedCount == NoDrift)
            return new DatabaseSchemaCompatibilityResult(true, NoDrift, NoDrift, CompatibleDescription);

        var description = (pendingCount, unknownAppliedCount) switch
        {
            (_, NoDrift) => PendingMigrationsDescription,
            (NoDrift, _) => UnknownAppliedMigrationsDescription,
            _ => DivergentDescription
        };

        return new DatabaseSchemaCompatibilityResult(false, pendingCount, unknownAppliedCount, description);
    }
}

public sealed record DatabaseSchemaCompatibilityResult(
    bool IsCompatible,
    int PendingMigrationCount,
    int UnknownAppliedMigrationCount,
    string Description);
