using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public static class SchemaDriftCodes
{
    public const string ModelTableMissing = "model_table_missing";
    public const string ModelColumnMissing = "model_column_missing";
    public const string RowLevelSecurityNotEnabled = "row_level_security_not_enabled";
    public const string PolicyMissing = "policy_missing";
    public const string FunctionMissing = "function_missing";
    public const string FunctionOwnerMismatch = "function_owner_mismatch";
    public const string FunctionOwnerUndeclared = "function_owner_undeclared";
    public const string RoleMissing = "role_missing";
    public const string SecurityScriptUnreadable = "security_script_unreadable";
    public const string MetadataMissing = "schema_metadata_missing";
    public const string ModelFingerprintMismatch = "model_fingerprint_mismatch";
}

public sealed record SchemaDriftReport(
    string ModelFingerprint,
    string? RecordedFingerprint,
    int ModelTableCount,
    int PolicyCount,
    IReadOnlyList<string> FailureCodes,
    IReadOnlyList<string> Details)
{
    public bool IsCompatible => FailureCodes.Count == 0;
}

/// <summary>
/// Replaces the former migration-ledger and requirements-contract checks. Drift is measured against two
/// sources that ship with the assembly: the EF model (tables and columns) and <c>security.sql</c>
/// (roles, functions, row level security and policies). There is no second hand-maintained contract to
/// keep in sync, so the check cannot silently disagree with what the deploy actually applies.
/// </summary>
public static partial class PostgresSchemaDriftInspector
{
    // Scalar projection requires the column alias "Value"; EF maps any other name to no value at all.
    private const string ExistingTablesSql = """
        SELECT schemaname || '.' || tablename AS "Value"
        FROM pg_tables
        WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
        """;

    private const string ExistingColumnsSql = """
        SELECT table_schema || '.' || table_name || '.' || column_name AS "Value"
        FROM information_schema.columns
        WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
        """;

    private const string RowLevelSecurityTablesSql = """
        SELECT schemaname || '.' || tablename AS "Value"
        FROM pg_tables
        WHERE rowsecurity
        """;

    private const string PolicyNamesSql = "SELECT policyname AS \"Value\" FROM pg_policies";

    private const string FunctionNamesSql = """
        SELECT n.nspname || '.' || p.proname AS "Value"
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
        """;

    private const string FunctionOwnersSql = """
        SELECT n.nspname || '.' || p.proname || ':' || r.rolname AS "Value"
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        JOIN pg_roles r ON r.oid = p.proowner
        WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
        """;

    private const string RoleNamesSql = "SELECT rolname AS \"Value\" FROM pg_roles";

    private const string RecordedFingerprintSql = """
        SELECT model_fingerprint AS "Value"
        FROM ops.schema_metadata
        WHERE singleton
        """;

    [GeneratedRegex(@"ALTER TABLE\s+(?<target>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)?)\s+ENABLE ROW LEVEL SECURITY;")]
    private static partial Regex RowLevelSecurityDeclaration();

    [GeneratedRegex(@"CREATE POLICY\s+(?<name>[A-Za-z0-9_]+)\s+ON\s+(?<target>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)?)")]
    private static partial Regex PolicyDeclaration();

    [GeneratedRegex(@"CREATE OR REPLACE FUNCTION\s+(?<name>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)?)")]
    private static partial Regex FunctionDeclaration();

    [GeneratedRegex(@"ALTER FUNCTION\s+(?<name>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)?)\s*\([^)]*\)\s+OWNER TO\s+(?<owner>[A-Za-z0-9_]+)")]
    private static partial Regex FunctionOwnerDeclaration();

    [GeneratedRegex(@"CREATE ROLE\s+(?<name>[A-Za-z0-9_]+)")]
    private static partial Regex RoleDeclaration();

    public static async Task<SchemaDriftReport> InspectAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var failures = new List<string>();
        var details = new List<string>();

        string securityScript;
        try
        {
            securityScript = await PostgresSchemaBootstrap.ReadSecurityScriptAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new SchemaDriftReport(
                PostgresSchemaBootstrap.ComputeModelFingerprint(context),
                null,
                0,
                0,
                [SchemaDriftCodes.SecurityScriptUnreadable],
                ["The database security script is not embedded in this assembly."]);
        }

        var existingTables = await QueryAsync(context, ExistingTablesSql, cancellationToken).ConfigureAwait(false);
        var existingColumns = await QueryAsync(context, ExistingColumnsSql, cancellationToken).ConfigureAwait(false);
        var securedTables = await QueryAsync(context, RowLevelSecurityTablesSql, cancellationToken).ConfigureAwait(false);
        var existingPolicies = await QueryAsync(context, PolicyNamesSql, cancellationToken).ConfigureAwait(false);
        var existingFunctions = await QueryAsync(context, FunctionNamesSql, cancellationToken).ConfigureAwait(false);
        var functionOwners = await QueryOwnerMapAsync(context, FunctionOwnersSql, cancellationToken).ConfigureAwait(false);
        var existingRoles = await QueryAsync(context, RoleNamesSql, cancellationToken).ConfigureAwait(false);

        var modelTables = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
                continue;

            var schema = entityType.GetSchema() ?? entityType.GetDefaultSchema();
            var qualified = string.IsNullOrEmpty(schema) ? tableName : $"{schema}.{tableName}";
            if (!modelTables.TryGetValue(qualified, out var columns))
            {
                columns = [];
                modelTables[qualified] = columns;
            }

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName();
                if (!string.IsNullOrEmpty(column) && !columns.Contains(column))
                    columns.Add(column);
            }
        }

        foreach (var (table, columns) in modelTables)
        {
            if (!existingTables.Contains(table))
            {
                failures.Add(SchemaDriftCodes.ModelTableMissing);
                details.Add($"table missing: {table}");
                continue;
            }

            foreach (var column in columns)
            {
                if (!existingColumns.Contains($"{table}.{column}"))
                {
                    failures.Add(SchemaDriftCodes.ModelColumnMissing);
                    details.Add($"column missing: {table}.{column}");
                }
            }
        }

        foreach (var declared in RowLevelSecurityDeclaration().Matches(securityScript).Cast<Match>().Select(m => m.Groups["target"].Value))
        {
            if (!securedTables.Contains(declared))
            {
                failures.Add(SchemaDriftCodes.RowLevelSecurityNotEnabled);
                details.Add($"row level security not enabled: {declared}");
            }
        }

        var policyCount = 0;
        foreach (Match declaration in PolicyDeclaration().Matches(securityScript))
        {
            policyCount++;
            var name = declaration.Groups["name"].Value;
            if (!existingPolicies.Contains(name))
            {
                failures.Add(SchemaDriftCodes.PolicyMissing);
                details.Add($"policy missing: {name}");
            }
        }

        var declaredFunctions = FunctionDeclaration().Matches(securityScript).Cast<Match>()
            .Select(match => match.Groups["name"].Value).ToArray();
        var declaredOwners = FunctionOwnerDeclaration().Matches(securityScript).Cast<Match>()
            .Select(match => (Name: match.Groups["name"].Value, Owner: match.Groups["owner"].Value))
            .ToArray();
        foreach (var name in declaredFunctions)
        {
            if (!existingFunctions.Contains(name))
            {
                failures.Add(SchemaDriftCodes.FunctionMissing);
                details.Add($"function missing: {name}");
            }

            if (declaredOwners.All(owner => owner.Name != name))
            {
                failures.Add(SchemaDriftCodes.FunctionOwnerUndeclared);
                details.Add($"function owner undeclared: {name}");
            }
        }

        foreach (var (name, owner) in declaredOwners)
        {
            if (functionOwners.TryGetValue(name, out var actual) &&
                !string.Equals(actual, owner, StringComparison.Ordinal))
            {
                failures.Add(SchemaDriftCodes.FunctionOwnerMismatch);
                details.Add($"function owner mismatch: {name} expected {owner} actual {actual}");
            }
        }

        foreach (var name in RoleDeclaration().Matches(securityScript).Cast<Match>().Select(m => m.Groups["name"].Value))
        {
            if (!existingRoles.Contains(name))
            {
                failures.Add(SchemaDriftCodes.RoleMissing);
                details.Add($"role missing: {name}");
            }
        }

        var modelFingerprint = PostgresSchemaBootstrap.ComputeModelFingerprint(context);
        var recordedFingerprint = await TryReadRecordedFingerprintAsync(context, cancellationToken).ConfigureAwait(false);
        if (recordedFingerprint is null)
        {
            failures.Add(SchemaDriftCodes.MetadataMissing);
            details.Add("schema metadata row is absent; run dbinit");
        }
        else if (!string.Equals(recordedFingerprint, modelFingerprint, StringComparison.Ordinal))
        {
            failures.Add(SchemaDriftCodes.ModelFingerprintMismatch);
            details.Add("the deployed model does not match the model this database was initialized with; run dbinit");
        }

        return new SchemaDriftReport(
            modelFingerprint,
            recordedFingerprint,
            modelTables.Count,
            policyCount,
            failures.Distinct(StringComparer.Ordinal).ToArray(),
            details);
    }

    private static async Task<HashSet<string>> QueryAsync(
        PostgresDbContext context,
        string sql,
        CancellationToken cancellationToken) =>
        (await context.Database.SqlQueryRaw<string>(sql).ToListAsync(cancellationToken).ConfigureAwait(false))
        .ToHashSet(StringComparer.Ordinal);

    private static async Task<Dictionary<string, string>> QueryOwnerMapAsync(
        PostgresDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        var values = await context.Database.SqlQueryRaw<string>(sql).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var separator = value.LastIndexOf(':');
            if (separator <= 0 || separator == value.Length - 1)
                continue;
            owners[value[..separator]] = value[(separator + 1)..];
        }

        return owners;
    }

    private static async Task<string?> TryReadRecordedFingerprintAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.Database
                .SqlQueryRaw<string>(RecordedFingerprintSql)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A missing metadata table is reported as absent metadata rather than a provider failure.
            return null;
        }
    }
}
