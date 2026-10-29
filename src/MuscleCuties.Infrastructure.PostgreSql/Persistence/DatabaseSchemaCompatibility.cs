using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>
/// Stable failure classes consumed by the release pipeline. Publish any change to these names to
/// Orchestrator 1 before the pipeline starts branching on them.
/// </summary>
public static class SchemaFailureCodes
{
    public const string RequirementsHashMismatch = "SCHEMA_REQUIREMENTS_HASH_MISMATCH";
    public const string RequirementsInvalid = "SCHEMA_REQUIREMENTS_INVALID";
    public const string RequirementsStale = "SCHEMA_REQUIREMENTS_STALE";
    public const string ContractVersionUnsupported = "SCHEMA_CONTRACT_VERSION_UNSUPPORTED";
    public const string TargetIdentityMismatch = "TARGET_IDENTITY_MISMATCH";
    public const string TargetVersionUnsupported = "TARGET_VERSION_UNSUPPORTED";
    public const string DatabaseAccessUnverified = "DATABASE_ACCESS_UNVERIFIED";
    public const string RequiredSchemaMissing = "SCHEMA_REQUIRED_SCHEMA_MISSING";
    public const string RequiredTableMissing = "SCHEMA_REQUIRED_TABLE_MISSING";
    public const string RequiredColumnMissing = "SCHEMA_REQUIRED_COLUMN_MISSING";
    public const string TypeMismatch = "SCHEMA_TYPE_MISMATCH";
    public const string NullabilityMismatch = "SCHEMA_NULLABILITY_MISMATCH";
    public const string DefaultMismatch = "SCHEMA_DEFAULT_MISMATCH";
    public const string RequiredConstraintMissing = "SCHEMA_REQUIRED_CONSTRAINT_MISSING";
    public const string ConstraintMismatch = "SCHEMA_CONSTRAINT_MISMATCH";
    public const string RequiredIndexMissing = "SCHEMA_REQUIRED_INDEX_MISSING";
    public const string IndexMismatch = "SCHEMA_INDEX_MISMATCH";
    public const string IndexNotReady = "SCHEMA_INDEX_NOT_READY";
    public const string RlsMismatch = "SCHEMA_RLS_MISMATCH";
    public const string PolicyMissing = "SCHEMA_POLICY_MISSING";
    public const string PolicyMismatch = "SCHEMA_POLICY_MISMATCH";
    public const string FunctionMissing = "SCHEMA_FUNCTION_MISSING";
    public const string FunctionSecurityMismatch = "SCHEMA_FUNCTION_SECURITY_MISMATCH";
    public const string RoleMissing = "SCHEMA_ROLE_MISSING";
    public const string RoleAttributeMismatch = "SCHEMA_ROLE_ATTRIBUTE_MISMATCH";
    public const string RoleMembershipMismatch = "SCHEMA_ROLE_MEMBERSHIP_MISMATCH";
    public const string GrantMismatch = "SCHEMA_GRANT_MISMATCH";
    public const string MigrationHistoryMissing = "MIGRATION_HISTORY_MISSING";
    public const string MigrationHistoryPending = "MIGRATION_HISTORY_PENDING";
    public const string MigrationHistoryUnsupported = "MIGRATION_HISTORY_UNSUPPORTED";
    public const string MigrationHistoryInconsistent = "MIGRATION_HISTORY_INCONSISTENT";
    public const string ModelContractMismatch = "SCHEMA_MODEL_CONTRACT_MISMATCH";
    public const string EvaluationFailed = "SCHEMA_EVALUATION_FAILED";
}

public static class SchemaCompatibilityStatus
{
    public const string Compatible = "compatible";
    public const string Incompatible = "incompatible";
    public const string Unknown = "unknown";
}

public static class MigrationHistoryModes
{
    public const string Absent = "absent";
    public const string ExactSource = "exact-source";
    public const string Pending = "pending";
    public const string UnknownApplied = "unknown-applied";
    public const string NonPrefix = "non-prefix";
}

public sealed class SchemaRequirementsException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record SchemaIssue(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("expected")] string Expected,
    [property: JsonPropertyName("actual")] string Actual);

/// <summary>
/// Machine-readable CI contract. Property names are part of the integration contract with the
/// release pipeline; do not rename them without a contract version change.
/// </summary>
public sealed class SchemaCompatibilityResult
{
    [JsonPropertyName("contract_version")] public string ContractVersion { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = SchemaCompatibilityStatus.Unknown;
    [JsonPropertyName("requirements_sha256")] public string RequirementsSha256 { get; init; } = string.Empty;
    [JsonPropertyName("db_fingerprint_sha256")] public string DatabaseFingerprintSha256 { get; init; } = string.Empty;
    [JsonPropertyName("migration_history_mode")] public string MigrationHistoryMode { get; init; } = MigrationHistoryModes.Absent;
    [JsonPropertyName("failure_codes")] public IReadOnlyList<string> FailureCodes { get; init; } = [];
    [JsonPropertyName("source_migration_count")] public int SourceMigrationCount { get; init; }
    [JsonPropertyName("applied_migration_count")] public int AppliedMigrationCount { get; init; }
    [JsonPropertyName("target_database")] public string TargetDatabase { get; init; } = string.Empty;
    [JsonPropertyName("server_version")] public string ServerVersion { get; init; } = string.Empty;
    [JsonPropertyName("issues")] public IReadOnlyList<SchemaIssue> Issues { get; init; } = [];

    [JsonIgnore]
    public int ExitCode => Status switch
    {
        SchemaCompatibilityStatus.Compatible => 0,
        SchemaCompatibilityStatus.Incompatible => 1,
        _ => 2
    };
}

public sealed class SchemaRequirements
{
    [JsonPropertyName("contractVersion")] public string ContractVersion { get; set; } = string.Empty;
    [JsonPropertyName("generatedFrom")] public SchemaGeneratedFrom GeneratedFrom { get; set; } = new();
    [JsonPropertyName("target")] public SchemaTargetRequirements Target { get; set; } = new();
    [JsonPropertyName("migrationHistory")] public SchemaHistoryRequirements MigrationHistory { get; set; } = new();
    [JsonPropertyName("requiredSchemas")] public List<string> RequiredSchemas { get; set; } = [];
    [JsonPropertyName("modelContract")] public SchemaModelContractRequirements ModelContract { get; set; } = new();
    [JsonPropertyName("requiredTables")] public List<SchemaTableRequirement> RequiredTables { get; set; } = [];
    [JsonPropertyName("pinnedColumns")] public List<SchemaColumnRequirement> PinnedColumns { get; set; } = [];
    [JsonPropertyName("pinnedIndexes")] public List<SchemaIndexRequirement> PinnedIndexes { get; set; } = [];
    [JsonPropertyName("pinnedConstraints")] public List<SchemaConstraintRequirement> PinnedConstraints { get; set; } = [];
    [JsonPropertyName("rowLevelSecurity")] public List<SchemaRlsRequirement> RowLevelSecurity { get; set; } = [];
    [JsonPropertyName("policies")] public List<SchemaPolicyRequirement> Policies { get; set; } = [];
    [JsonPropertyName("functions")] public List<SchemaFunctionRequirement> Functions { get; set; } = [];
    [JsonPropertyName("roles")] public List<SchemaRoleRequirement> Roles { get; set; } = [];
    [JsonPropertyName("privileges")] public List<SchemaPrivilegeRequirement> Privileges { get; set; } = [];

    [JsonIgnore] public string SourceSha256 { get; set; } = string.Empty;
}

public sealed class SchemaGeneratedFrom
{
    [JsonPropertyName("sourceReference")] public string SourceReference { get; set; } = string.Empty;
    [JsonPropertyName("sourceMigrationCount")] public int SourceMigrationCount { get; set; }
    [JsonPropertyName("finalMigrationId")] public string FinalMigrationId { get; set; } = string.Empty;
}

public sealed class SchemaTargetRequirements
{
    [JsonPropertyName("allowedDatabases")] public List<string> AllowedDatabases { get; set; } = [];
    [JsonPropertyName("serverVersionMinimum")] public string ServerVersionMinimum { get; set; } = string.Empty;
    [JsonPropertyName("serverVersionMaximumExclusive")] public string ServerVersionMaximumExclusive { get; set; } = string.Empty;
}

public sealed class SchemaHistoryRequirements
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "public";
    [JsonPropertyName("table")] public string Table { get; set; } = "__EFMigrationsHistory";
    [JsonPropertyName("acceptedObservedModes")] public List<string> AcceptedObservedModes { get; set; } = [];
    [JsonPropertyName("expectedAppliedCount")] public int ExpectedAppliedCount { get; set; }
    [JsonPropertyName("expectedFinalMigrationId")] public string ExpectedFinalMigrationId { get; set; } = string.Empty;
}

public sealed class SchemaModelContractRequirements
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("expectedTableCount")] public int ExpectedTableCount { get; set; }
    [JsonPropertyName("ignoredIndexes")] public List<SchemaIgnoredModelIndex> IgnoredIndexes { get; set; } = [];
}

/// <summary>A reviewed, documented exception where the model snapshot is stale relative to the migrations.</summary>
public sealed class SchemaIgnoredModelIndex
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("columns")] public List<string> Columns { get; set; } = [];
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class SchemaTableRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("owner")] public string? Owner { get; set; }
}

public sealed class SchemaColumnRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("column")] public string Column { get; set; } = string.Empty;
    [JsonPropertyName("dataType")] public string DataType { get; set; } = string.Empty;
    [JsonPropertyName("nullable")] public bool Nullable { get; set; }
    [JsonPropertyName("defaultSql")] public string? DefaultSql { get; set; }
}

public sealed class SchemaIndexRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("index")] public string Index { get; set; } = string.Empty;
    [JsonPropertyName("columns")] public List<string> Columns { get; set; } = [];
    [JsonPropertyName("unique")] public bool Unique { get; set; }
}

public sealed class SchemaConstraintRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("constraint")] public string Constraint { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("columns")] public List<string> Columns { get; set; } = [];
}

public sealed class SchemaRlsRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("forced")] public bool Forced { get; set; } = true;
}

public sealed class SchemaPolicyRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; set; } = string.Empty;
    [JsonPropertyName("policy")] public string Policy { get; set; } = string.Empty;
    [JsonPropertyName("command")] public string Command { get; set; } = "ALL";
    [JsonPropertyName("permissive")] public bool Permissive { get; set; } = true;
    [JsonPropertyName("roles")] public List<string> Roles { get; set; } = [];
}

public sealed class SchemaFunctionRequirement
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;
    [JsonPropertyName("securityDefiner")] public bool SecurityDefiner { get; set; }
    [JsonPropertyName("owner")] public string? Owner { get; set; }
    [JsonPropertyName("searchPath")] public List<string> SearchPath { get; set; } = [];
}

public sealed class SchemaRoleRequirement
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("login")] public bool Login { get; set; }
    [JsonPropertyName("superuser")] public bool Superuser { get; set; }
    [JsonPropertyName("bypassRls")] public bool BypassRls { get; set; }
    [JsonPropertyName("createDb")] public bool CreateDb { get; set; }
    [JsonPropertyName("createRole")] public bool CreateRole { get; set; }
    [JsonPropertyName("forbiddenMemberships")] public List<string> ForbiddenMemberships { get; set; } = [];
}

public sealed class SchemaPrivilegeRequirement
{
    [JsonPropertyName("objectType")] public string ObjectType { get; set; } = "table";
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("object")] public string Object { get; set; } = string.Empty;
    [JsonPropertyName("columns")] public List<string> Columns { get; set; } = [];
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;
    [JsonPropertyName("privilege")] public string Privilege { get; set; } = string.Empty;
}

public sealed record MigrationHistoryState(string Mode, IReadOnlyList<string> PendingIds);

/// <summary>
/// Read-only physical compatibility evaluator. It never issues DDL or DML, never creates a
/// migration, and never trusts EF pending-model state as proof that the physical schema is correct.
/// </summary>
public static class DatabaseSchemaCompatibility
{
    public const string SupportedContractVersion = "1.0.0";

    private const string MetadataOnlyConnection =
        "Host=127.0.0.1;Port=1;Database=model_metadata_only;Username=model_metadata_only;Timeout=1;CommandTimeout=1";

    private static readonly Lazy<IReadOnlyList<string>> SourceMigrationIds = new(ReadSourceMigrationIds);

    private static readonly Lazy<ModelContract> Model = new(BuildModelContract, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<string> GetSourceMigrationIds() => SourceMigrationIds.Value;

    public static string ComputeFileSha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public static SchemaRequirements LoadRequirements(string path, string expectedSha256)
    {
        if (!File.Exists(path))
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "The schema requirements manifest does not exist.");

        var actualSha256 = ComputeFileSha256(path);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsHashMismatch,
                "The schema requirements manifest SHA-256 does not match the supplied checksum.");

        SchemaRequirements requirements;
        try
        {
            requirements = JsonSerializer.Deserialize<SchemaRequirements>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "The schema requirements manifest is empty.");
        }
        catch (JsonException)
        {
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "The schema requirements manifest is not valid JSON.");
        }

        ValidateRequirements(requirements);
        requirements.SourceSha256 = actualSha256;
        return requirements;
    }

    /// <summary>
    /// Computes the observed migration-history mode without contacting a database. Applied IDs must
    /// be ordered exactly as the EF history table returns them.
    /// </summary>
    public static MigrationHistoryState ComputeMigrationHistoryState(
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> appliedIds)
    {
        if (appliedIds.Count == 0)
            return new MigrationHistoryState(MigrationHistoryModes.Absent, []);

        var source = sourceIds.ToArray();
        var applied = appliedIds.ToArray();
        var unknown = applied.Where(id => !source.Contains(id, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0)
            return new MigrationHistoryState(MigrationHistoryModes.UnknownApplied, unknown);

        for (var index = 0; index < applied.Length; index++)
        {
            if (!string.Equals(applied[index], source[index], StringComparison.Ordinal))
                return new MigrationHistoryState(MigrationHistoryModes.NonPrefix,
                    source.Where(id => !applied.Contains(id, StringComparer.Ordinal)).ToArray());
        }

        if (applied.Length == source.Length)
            return new MigrationHistoryState(MigrationHistoryModes.ExactSource, []);

        return new MigrationHistoryState(MigrationHistoryModes.Pending,
            source.Skip(applied.Length).ToArray());
    }

    public static async Task<SchemaCompatibilityResult> EvaluateAsync(
        string connectionString,
        SchemaRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await CatalogSnapshot.CollectAsync(connectionString, requirements, cancellationToken);
            return Evaluate(snapshot, requirements);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never let a provider message reach CI: it may embed the connection string, host names
            // or SQL text. The exception type alone is enough to diagnose the access class.
            return new SchemaCompatibilityResult
            {
                ContractVersion = requirements.ContractVersion,
                Status = SchemaCompatibilityStatus.Unknown,
                RequirementsSha256 = requirements.SourceSha256,
                MigrationHistoryMode = MigrationHistoryModes.Absent,
                FailureCodes = [SchemaFailureCodes.DatabaseAccessUnverified],
                SourceMigrationCount = SourceMigrationIds.Value.Count,
                Issues =
                [
                    new SchemaIssue(SchemaFailureCodes.DatabaseAccessUnverified, "database",
                        "read-only metadata access", exception.GetType().Name)
                ]
            };
        }
    }

    public static SchemaCompatibilityResult CreateUnknownResult(
        SchemaRequirements? requirements,
        string failureCode,
        string detail)
    {
        return new SchemaCompatibilityResult
        {
            ContractVersion = requirements?.ContractVersion ?? string.Empty,
            Status = SchemaCompatibilityStatus.Unknown,
            RequirementsSha256 = requirements?.SourceSha256 ?? string.Empty,
            MigrationHistoryMode = MigrationHistoryModes.Absent,
            FailureCodes = [failureCode],
            SourceMigrationCount = SourceMigrationIds.Value.Count,
            Issues = [new SchemaIssue(failureCode, "requirements", "valid contract", detail)]
        };
    }

    private static SchemaCompatibilityResult Evaluate(CatalogSnapshot snapshot, SchemaRequirements requirements)
    {
        var issues = new List<SchemaIssue>();
        var history = ComputeMigrationHistoryState(SourceMigrationIds.Value, snapshot.AppliedMigrationIds);

        EvaluateTarget(snapshot, requirements, issues);
        EvaluateSchemas(snapshot, requirements, issues);
        EvaluateModelContract(snapshot, requirements, issues);
        EvaluatePinnedTables(snapshot, requirements, issues);
        EvaluatePinnedColumns(snapshot, requirements, issues);
        EvaluatePinnedIndexes(snapshot, requirements, issues);
        EvaluatePinnedConstraints(snapshot, requirements, issues);
        EvaluateRowLevelSecurity(snapshot, requirements, issues);
        EvaluatePolicies(snapshot, requirements, issues);
        EvaluateFunctions(snapshot, requirements, issues);
        EvaluateRoles(snapshot, requirements, issues);
        EvaluatePrivileges(snapshot, requirements, issues);
        EvaluateMigrationHistory(snapshot, requirements, history, issues);

        var orderedIssues = issues
            .GroupBy(issue => (issue.Code, issue.Object))
            .Select(group => group.First())
            .OrderBy(issue => issue.Object, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        var failureCodes = orderedIssues.Select(issue => issue.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        return new SchemaCompatibilityResult
        {
            ContractVersion = requirements.ContractVersion,
            Status = orderedIssues.Length == 0 ? SchemaCompatibilityStatus.Compatible : SchemaCompatibilityStatus.Incompatible,
            RequirementsSha256 = requirements.SourceSha256,
            DatabaseFingerprintSha256 = snapshot.FingerprintSha256,
            MigrationHistoryMode = history.Mode,
            FailureCodes = failureCodes,
            SourceMigrationCount = SourceMigrationIds.Value.Count,
            AppliedMigrationCount = snapshot.AppliedMigrationIds.Count,
            TargetDatabase = snapshot.DatabaseName,
            ServerVersion = snapshot.ServerVersion,
            Issues = orderedIssues
        };
    }

    private static void EvaluateTarget(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        if (requirements.Target.AllowedDatabases.Count > 0 &&
            !requirements.Target.AllowedDatabases.Contains(snapshot.DatabaseName, StringComparer.Ordinal))
            Fail(issues, SchemaFailureCodes.TargetIdentityMismatch, $"database:{snapshot.DatabaseName}",
                string.Join(",", requirements.Target.AllowedDatabases), snapshot.DatabaseName);

        if (!string.IsNullOrWhiteSpace(requirements.Target.ServerVersionMinimum))
        {
            var minimum = ParseVersion(requirements.Target.ServerVersionMinimum);
            if (snapshot.ServerVersionNumber < minimum)
                Fail(issues, SchemaFailureCodes.TargetVersionUnsupported, "server_version",
                    requirements.Target.ServerVersionMinimum, snapshot.ServerVersion);
        }

        if (!string.IsNullOrWhiteSpace(requirements.Target.ServerVersionMaximumExclusive))
        {
            var maximum = ParseVersion(requirements.Target.ServerVersionMaximumExclusive);
            if (snapshot.ServerVersionNumber >= maximum)
                Fail(issues, SchemaFailureCodes.TargetVersionUnsupported, "server_version",
                    $"<{requirements.Target.ServerVersionMaximumExclusive}", snapshot.ServerVersion);
        }
    }

    private static void EvaluateSchemas(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var schema in requirements.RequiredSchemas)
            if (!snapshot.Schemas.Contains(schema))
                Fail(issues, SchemaFailureCodes.RequiredSchemaMissing, schema, "present", "missing");
    }

    private static void EvaluateModelContract(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        if (!requirements.ModelContract.Enabled)
            return;

        var model = Model.Value;
        if (requirements.ModelContract.ExpectedTableCount > 0 && model.Tables.Count != requirements.ModelContract.ExpectedTableCount)
            Fail(issues, SchemaFailureCodes.ModelContractMismatch, "model.tables",
                requirements.ModelContract.ExpectedTableCount.ToString(CultureInfo.InvariantCulture),
                model.Tables.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var table in model.Tables.Values.OrderBy(table => table.Schema, StringComparer.Ordinal)
                     .ThenBy(table => table.Table, StringComparer.Ordinal))
        {
            var objectName = $"{table.Schema}.{table.Table}";
            if (!snapshot.Tables.TryGetValue((table.Schema, table.Table), out var physical))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, objectName, "present", "missing");
                continue;
            }

            foreach (var column in table.Columns.Values.OrderBy(column => column.Name, StringComparer.Ordinal))
            {
                var columnObject = $"{objectName}.{column.Name}";
                if (!physical.Columns.TryGetValue(column.Name, out var physicalColumn))
                {
                    Fail(issues, SchemaFailureCodes.RequiredColumnMissing, columnObject, column.DataType, "missing");
                    continue;
                }

                if (!string.Equals(NormalizeType(column.DataType), NormalizeType(physicalColumn.DataType), StringComparison.Ordinal))
                    Fail(issues, SchemaFailureCodes.TypeMismatch, columnObject, column.DataType, physicalColumn.DataType);

                if (column.Nullable != physicalColumn.Nullable)
                    Fail(issues, SchemaFailureCodes.NullabilityMismatch, columnObject,
                        column.Nullable ? "nullable" : "not null",
                        physicalColumn.Nullable ? "nullable" : "not null");

                if (!string.IsNullOrWhiteSpace(column.DefaultSql) &&
                    !string.Equals(NormalizeDefault(column.DefaultSql), NormalizeDefault(physicalColumn.DefaultSql), StringComparison.Ordinal))
                    Fail(issues, SchemaFailureCodes.DefaultMismatch, columnObject, column.DefaultSql!, physicalColumn.DefaultSql ?? "none");
            }

            if (table.PrimaryKey.Count > 0)
            {
                var physicalKey = physical.Constraints.Values.FirstOrDefault(constraint => constraint.Type == "p");
                if (physicalKey is null)
                    Fail(issues, SchemaFailureCodes.RequiredConstraintMissing, $"{objectName}:primary-key",
                        string.Join(",", table.PrimaryKey), "missing");
                else if (!NormalizeColumns(physicalKey.Columns).SequenceEqual(NormalizeColumns(table.PrimaryKey), StringComparer.Ordinal))
                    Fail(issues, SchemaFailureCodes.ConstraintMismatch, $"{objectName}:primary-key",
                        string.Join(",", table.PrimaryKey), string.Join(",", physicalKey.Columns));
            }

            foreach (var unique in table.UniqueKeys)
                if (!physical.Constraints.Values.Any(constraint =>
                        constraint.Type == "u" &&
                        NormalizeColumns(constraint.Columns).SequenceEqual(NormalizeColumns(unique), StringComparer.Ordinal)))
                    Fail(issues, SchemaFailureCodes.RequiredConstraintMissing, $"{objectName}:unique({string.Join(",", unique)})",
                        "present", "missing");

            foreach (var foreignKey in table.ForeignKeys)
            {
                var foreignObject = $"{objectName}:fk({string.Join(",", foreignKey.Columns)})";
                var reference = $"{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}({string.Join(",", foreignKey.ReferencedColumns)})";
                if (!physical.Constraints.Values.Any(constraint =>
                        constraint.Type == "f" &&
                        NormalizeColumns(constraint.Columns).SequenceEqual(NormalizeColumns(foreignKey.Columns), StringComparer.Ordinal) &&
                        string.Equals(constraint.ReferencedSchema, foreignKey.ReferencedSchema, StringComparison.Ordinal) &&
                        string.Equals(constraint.ReferencedTable, foreignKey.ReferencedTable, StringComparison.Ordinal) &&
                        NormalizeColumns(constraint.ReferencedColumns).SequenceEqual(NormalizeColumns(foreignKey.ReferencedColumns), StringComparer.Ordinal)))
                    Fail(issues, SchemaFailureCodes.RequiredConstraintMissing, foreignObject, reference, "missing");
            }

            foreach (var index in table.Indexes)
            {
                if (IsIgnoredModelIndex(table, index, requirements))
                    continue;

                var indexObject = $"{objectName}:index({string.Join(",", index.Columns)})";
                var candidate = physical.Indexes.Values.FirstOrDefault(physicalIndex =>
                    physicalIndex.Unique == index.Unique &&
                    NormalizeColumns(physicalIndex.Columns).SequenceEqual(NormalizeColumns(index.Columns), StringComparer.Ordinal) &&
                    string.Equals(NormalizeDefault(physicalIndex.Predicate), NormalizeDefault(index.Predicate), StringComparison.Ordinal));
                if (candidate is null)
                    Fail(issues, SchemaFailureCodes.RequiredIndexMissing, indexObject, "present", "missing");
                else if (!candidate.Valid || !candidate.Ready)
                    Fail(issues, SchemaFailureCodes.IndexNotReady, $"{objectName}:{candidate.Name}",
                        "valid,ready", $"{candidate.Valid},{candidate.Ready}");
            }
        }
    }

    private static void EvaluatePinnedTables(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var table in requirements.RequiredTables)
        {
            var objectName = $"{table.Schema}.{table.Table}";
            if (!snapshot.Tables.TryGetValue((table.Schema, table.Table), out var physical))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, objectName, "present", "missing");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(table.Owner) &&
                !string.Equals(table.Owner, physical.Owner, StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.GrantMismatch, objectName, $"owner:{table.Owner}", $"owner:{physical.Owner}");
        }
    }

    private static void EvaluatePinnedColumns(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var column in requirements.PinnedColumns)
        {
            var objectName = $"{column.Schema}.{column.Table}.{column.Column}";
            if (!snapshot.Tables.TryGetValue((column.Schema, column.Table), out var table))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, $"{column.Schema}.{column.Table}", "present", "missing");
                continue;
            }

            if (!table.Columns.TryGetValue(column.Column, out var physical))
            {
                Fail(issues, SchemaFailureCodes.RequiredColumnMissing, objectName, column.DataType, "missing");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(column.DataType) &&
                !string.Equals(NormalizeType(column.DataType), NormalizeType(physical.DataType), StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.TypeMismatch, objectName, column.DataType, physical.DataType);

            if (column.Nullable != physical.Nullable)
                Fail(issues, SchemaFailureCodes.NullabilityMismatch, objectName,
                    column.Nullable ? "nullable" : "not null",
                    physical.Nullable ? "nullable" : "not null");

            if (column.DefaultSql is not null &&
                !string.Equals(NormalizeDefault(column.DefaultSql), NormalizeDefault(physical.DefaultSql), StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.DefaultMismatch, objectName, column.DefaultSql, physical.DefaultSql ?? "none");
        }
    }

    private static void EvaluatePinnedIndexes(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var index in requirements.PinnedIndexes)
        {
            var objectName = $"{index.Schema}.{index.Table}.{index.Index}";
            if (!snapshot.Tables.TryGetValue((index.Schema, index.Table), out var table))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, $"{index.Schema}.{index.Table}", "present", "missing");
                continue;
            }

            if (!table.Indexes.TryGetValue(index.Index, out var physical))
            {
                Fail(issues, SchemaFailureCodes.RequiredIndexMissing, objectName, "present", "missing");
                continue;
            }

            if (!NormalizeColumns(physical.Columns).SequenceEqual(NormalizeColumns(index.Columns), StringComparer.Ordinal))
                Fail(issues, SchemaFailureCodes.IndexMismatch, objectName,
                    string.Join(",", index.Columns), string.Join(",", physical.Columns));

            if (physical.Unique != index.Unique)
                Fail(issues, SchemaFailureCodes.IndexMismatch, objectName,
                    index.Unique ? "unique" : "non-unique", physical.Unique ? "unique" : "non-unique");

            if (!physical.Valid || !physical.Ready)
                Fail(issues, SchemaFailureCodes.IndexNotReady, objectName, "valid,ready", $"{physical.Valid},{physical.Ready}");
        }
    }

    private static void EvaluatePinnedConstraints(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var constraint in requirements.PinnedConstraints)
        {
            var objectName = $"{constraint.Schema}.{constraint.Table}.{constraint.Constraint}";
            if (!snapshot.Tables.TryGetValue((constraint.Schema, constraint.Table), out var table))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, $"{constraint.Schema}.{constraint.Table}", "present", "missing");
                continue;
            }

            if (!table.Constraints.TryGetValue(constraint.Constraint, out var physical))
            {
                Fail(issues, SchemaFailureCodes.RequiredConstraintMissing, objectName, "present", "missing");
                continue;
            }

            if (!string.Equals(constraint.Type, physical.Type, StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.ConstraintMismatch, objectName, constraint.Type, physical.Type);

            if (constraint.Columns.Count > 0 &&
                !NormalizeColumns(physical.Columns).SequenceEqual(NormalizeColumns(constraint.Columns), StringComparer.Ordinal))
                Fail(issues, SchemaFailureCodes.ConstraintMismatch, objectName,
                    string.Join(",", constraint.Columns), string.Join(",", physical.Columns));
        }
    }

    private static void EvaluateRowLevelSecurity(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var requirement in requirements.RowLevelSecurity)
        {
            var objectName = $"{requirement.Schema}.{requirement.Table}";
            if (!snapshot.Tables.TryGetValue((requirement.Schema, requirement.Table), out var table))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, objectName, "present", "missing");
                continue;
            }

            if (table.RowLevelSecurity != requirement.Enabled)
                Fail(issues, SchemaFailureCodes.RlsMismatch, objectName,
                    requirement.Enabled ? "enabled" : "disabled", table.RowLevelSecurity ? "enabled" : "disabled");

            if (table.ForceRowLevelSecurity != requirement.Forced)
                Fail(issues, SchemaFailureCodes.RlsMismatch, objectName,
                    requirement.Forced ? "forced" : "not forced", table.ForceRowLevelSecurity ? "forced" : "not forced");
        }
    }

    private static void EvaluatePolicies(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var requirement in requirements.Policies)
        {
            var objectName = $"{requirement.Schema}.{requirement.Table}.{requirement.Policy}";
            if (!snapshot.Tables.TryGetValue((requirement.Schema, requirement.Table), out var table))
            {
                Fail(issues, SchemaFailureCodes.RequiredTableMissing, $"{requirement.Schema}.{requirement.Table}", "present", "missing");
                continue;
            }

            if (!table.Policies.TryGetValue(requirement.Policy, out var policy))
            {
                Fail(issues, SchemaFailureCodes.PolicyMissing, objectName, "present", "missing");
                continue;
            }

            if (!string.Equals(requirement.Command, policy.Command, StringComparison.OrdinalIgnoreCase))
                Fail(issues, SchemaFailureCodes.PolicyMismatch, objectName, requirement.Command, policy.Command);

            if (requirement.Permissive != policy.Permissive)
                Fail(issues, SchemaFailureCodes.PolicyMismatch, objectName,
                    requirement.Permissive ? "permissive" : "restrictive",
                    policy.Permissive ? "permissive" : "restrictive");

            if (requirement.Roles.Count > 0 &&
                !policy.Roles.Order(StringComparer.Ordinal).SequenceEqual(requirement.Roles.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                Fail(issues, SchemaFailureCodes.PolicyMismatch, objectName,
                    string.Join(",", requirement.Roles), string.Join(",", policy.Roles));
        }
    }

    private static void EvaluateFunctions(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var requirement in requirements.Functions)
        {
            var objectName = $"{requirement.Schema}.{requirement.Name}({requirement.Arguments})";
            var function = snapshot.Functions.FirstOrDefault(candidate =>
                string.Equals(candidate.Schema, requirement.Schema, StringComparison.Ordinal) &&
                string.Equals(candidate.Name, requirement.Name, StringComparison.Ordinal) &&
                string.Equals(candidate.Arguments, requirement.Arguments, StringComparison.Ordinal));
            if (function is null)
            {
                Fail(issues, SchemaFailureCodes.FunctionMissing, objectName, "present", "missing");
                continue;
            }

            if (function.SecurityDefiner != requirement.SecurityDefiner)
                Fail(issues, SchemaFailureCodes.FunctionSecurityMismatch, objectName,
                    requirement.SecurityDefiner ? "security definer" : "security invoker",
                    function.SecurityDefiner ? "security definer" : "security invoker");

            if (!string.IsNullOrWhiteSpace(requirement.Owner) &&
                !string.Equals(requirement.Owner, function.Owner, StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.FunctionSecurityMismatch, objectName,
                    $"owner:{requirement.Owner}", $"owner:{function.Owner}");

            if (requirement.SearchPath.Count > 0 &&
                !string.Equals(CanonicalSearchPath(requirement.SearchPath), CanonicalSearchPath(function.SearchPath), StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.FunctionSecurityMismatch, objectName,
                    CanonicalSearchPath(requirement.SearchPath), CanonicalSearchPath(function.SearchPath));
        }
    }

    private static void EvaluateRoles(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var requirement in requirements.Roles)
        {
            if (!snapshot.Roles.TryGetValue(requirement.Name, out var role))
            {
                Fail(issues, SchemaFailureCodes.RoleMissing, $"role:{requirement.Name}", "present", "missing");
                continue;
            }

            var observed = new SchemaRoleRequirement
            {
                Login = role.Login,
                Superuser = role.Superuser,
                BypassRls = role.BypassRls,
                CreateDb = role.CreateDb,
                CreateRole = role.CreateRole
            };
            if (observed.Login != requirement.Login || observed.Superuser != requirement.Superuser ||
                observed.BypassRls != requirement.BypassRls || observed.CreateDb != requirement.CreateDb ||
                observed.CreateRole != requirement.CreateRole)
                Fail(issues, SchemaFailureCodes.RoleAttributeMismatch, $"role:{requirement.Name}",
                    DescribeRoleAttributes(requirement), DescribeRoleAttributes(observed));

            foreach (var forbidden in requirement.ForbiddenMemberships)
                if (role.Memberships.Contains(forbidden, StringComparer.Ordinal))
                    Fail(issues, SchemaFailureCodes.RoleMembershipMismatch, $"role:{requirement.Name}",
                        $"not member of {forbidden}", $"member of {forbidden}");
        }
    }

    private static void EvaluatePrivileges(CatalogSnapshot snapshot, SchemaRequirements requirements, List<SchemaIssue> issues)
    {
        foreach (var requirement in requirements.Privileges)
        {
            var objectName = DescribePrivilegeObject(requirement);
            var privileges = requirement.Privilege.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var privilege in privileges)
            {
                var granted = snapshot.HasPrivilege(requirement, privilege);
                if (granted is null)
                    continue;
                if (granted == false)
                    Fail(issues, SchemaFailureCodes.GrantMismatch, objectName,
                        $"{requirement.Role}:{privilege}", "not granted");
            }
        }
    }

    private static void EvaluateMigrationHistory(
        CatalogSnapshot snapshot,
        SchemaRequirements requirements,
        MigrationHistoryState history,
        List<SchemaIssue> issues)
    {
        var accepted = requirements.MigrationHistory.AcceptedObservedModes;
        if (accepted.Count == 0 || !accepted.Contains(history.Mode, StringComparer.Ordinal))
        {
            var code = history.Mode switch
            {
                MigrationHistoryModes.Pending => SchemaFailureCodes.MigrationHistoryPending,
                MigrationHistoryModes.Absent => SchemaFailureCodes.MigrationHistoryMissing,
                MigrationHistoryModes.NonPrefix => SchemaFailureCodes.MigrationHistoryInconsistent,
                _ => SchemaFailureCodes.MigrationHistoryUnsupported
            };
            Fail(issues, code, "migration_history", string.Join(",", accepted), history.Mode);
        }

        if (!string.IsNullOrWhiteSpace(requirements.MigrationHistory.ExpectedFinalMigrationId))
        {
            var lastApplied = snapshot.AppliedMigrationIds.LastOrDefault();
            if (history.Mode == MigrationHistoryModes.ExactSource &&
                !string.Equals(lastApplied, requirements.MigrationHistory.ExpectedFinalMigrationId, StringComparison.Ordinal))
                Fail(issues, SchemaFailureCodes.MigrationHistoryUnsupported, "migration_history.final",
                    requirements.MigrationHistory.ExpectedFinalMigrationId, lastApplied ?? "none");
        }

        if (requirements.MigrationHistory.ExpectedAppliedCount > 0 &&
            snapshot.AppliedMigrationIds.Count != requirements.MigrationHistory.ExpectedAppliedCount)
            Fail(issues, SchemaFailureCodes.MigrationHistoryPending, "migration_history.count",
                requirements.MigrationHistory.ExpectedAppliedCount.ToString(CultureInfo.InvariantCulture),
                snapshot.AppliedMigrationIds.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static void ValidateRequirements(SchemaRequirements requirements)
    {
        if (!string.Equals(requirements.ContractVersion, SupportedContractVersion, StringComparison.Ordinal))
            throw new SchemaRequirementsException(SchemaFailureCodes.ContractVersionUnsupported,
                $"Supported contract version is {SupportedContractVersion}.");

        if (requirements.RequiredSchemas.Count == 0 || requirements.Roles.Count == 0 ||
            requirements.Functions.Count == 0 || requirements.MigrationHistory.AcceptedObservedModes.Count == 0)
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "The manifest must declare required schemas, roles, functions and accepted migration-history modes.");

        if (requirements.Target.AllowedDatabases.Count == 0)
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "The manifest must restrict the target database identity.");

        var sourceIds = SourceMigrationIds.Value;
        if (requirements.GeneratedFrom.SourceMigrationCount != sourceIds.Count ||
            !string.Equals(requirements.GeneratedFrom.FinalMigrationId, sourceIds[^1], StringComparison.Ordinal))
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsStale,
                "The manifest source migration inventory no longer matches the compiled migration assembly.");

        if (requirements.MigrationHistory.AcceptedObservedModes.Contains(MigrationHistoryModes.ExactSource, StringComparer.Ordinal) &&
            (requirements.MigrationHistory.ExpectedAppliedCount != sourceIds.Count ||
             !string.Equals(requirements.MigrationHistory.ExpectedFinalMigrationId, sourceIds[^1], StringComparison.Ordinal)))
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsStale,
                "Exact-source history policy must pin the full source migration count and final migration ID.");

        EnsureUnique(requirements.PinnedColumns.Select(column => $"{column.Schema}.{column.Table}.{column.Column}"), "pinned column");
        EnsureUnique(requirements.PinnedIndexes.Select(index => $"{index.Schema}.{index.Table}.{index.Index}"), "pinned index");
        EnsureUnique(requirements.PinnedConstraints.Select(constraint => $"{constraint.Schema}.{constraint.Table}.{constraint.Constraint}"), "pinned constraint");
        EnsureUnique(requirements.RowLevelSecurity.Select(table => $"{table.Schema}.{table.Table}"), "row-level-security table");
        EnsureUnique(requirements.Policies.Select(policy => $"{policy.Schema}.{policy.Table}.{policy.Policy}"), "policy");
        EnsureUnique(requirements.Functions.Select(function => $"{function.Schema}.{function.Name}({function.Arguments})"), "function");
        EnsureUnique(requirements.Roles.Select(role => role.Name), "role");
        if (requirements.ModelContract.IgnoredIndexes.Any(index => string.IsNullOrWhiteSpace(index.Reason)))
            throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                "Every ignored model index needs a reviewed reason.");
    }

    private static void EnsureUnique(IEnumerable<string> keys, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
            if (!seen.Add(key))
                throw new SchemaRequirementsException(SchemaFailureCodes.RequirementsInvalid,
                    $"The manifest repeats the {label} {key}.");
    }

    private static int ParseVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var major = int.Parse(parts[0], CultureInfo.InvariantCulture);
        return major * 10_000 + (parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0);
    }

    private static string DescribeRoleAttributes(SchemaRoleRequirement role) =>
        $"login={role.Login};super={role.Superuser};bypassrls={role.BypassRls};createdb={role.CreateDb};createrole={role.CreateRole}";

    private static string DescribePrivilegeObject(SchemaPrivilegeRequirement requirement) => requirement.ObjectType switch
    {
        "schema" => $"schema:{requirement.Schema}",
        "function" => $"{requirement.Schema}.{requirement.Object}({requirement.Arguments})",
        "table" when requirement.Columns.Count > 0 =>
            $"column:{requirement.Schema}.{requirement.Object}({string.Join(",", requirement.Columns)})",
        _ => $"table:{requirement.Schema}.{requirement.Object}"
    };

    private static string CanonicalSearchPath(IEnumerable<string> values) =>
        string.Join(",", values
            .Select(value => value.Contains('=') ? value[(value.IndexOf('=') + 1)..] : value)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0));

    private static bool IsIgnoredModelIndex(ModelTable table, ModelIndex index, SchemaRequirements requirements) =>
        requirements.ModelContract.IgnoredIndexes.Any(ignored =>
            string.Equals(ignored.Schema, table.Schema, StringComparison.Ordinal) &&
            string.Equals(ignored.Table, table.Table, StringComparison.Ordinal) &&
            NormalizeColumns(ignored.Columns).SequenceEqual(NormalizeColumns(index.Columns), StringComparer.Ordinal));

    private static string[] NormalizeColumns(IEnumerable<string> columns) =>
        columns.Select(column => column.Trim().Trim('"')).ToArray();

    private static string NormalizeType(string? value)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("\"", string.Empty);
        type = Regex.Replace(type, @"\s+", " ");
        type = type switch
        {
            "int" or "int4" or "integer" => "integer",
            "int2" or "smallint" => "smallint",
            "int8" or "bigint" => "bigint",
            "bool" or "boolean" => "boolean",
            "float4" or "real" => "real",
            "float8" or "double precision" => "double precision",
            "decimal" or "numeric" => "numeric",
            "timestamptz" or "timestamp with time zone" => "timestamp with time zone",
            "timestamp" or "timestamp without time zone" => "timestamp without time zone",
            "timetz" or "time with time zone" => "time with time zone",
            "time" or "time without time zone" => "time without time zone",
            "varchar" => "character varying",
            _ => type
        };
        if (type.StartsWith("varchar(", StringComparison.Ordinal))
            type = "character varying(" + type["varchar(".Length..];
        if (type.StartsWith("decimal(", StringComparison.Ordinal))
            type = "numeric(" + type["decimal(".Length..];
        return type;
    }

    private static string NormalizeDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            while (text.StartsWith('(') && text.EndsWith(')') && IsBalanced(text[1..^1]))
            {
                text = text[1..^1].Trim();
                changed = true;
            }

            var cast = Regex.Match(text, @"::\s*[a-zA-Z_][a-zA-Z0-9_]*(?:\s+[a-zA-Z_]+)?(?:\[\])?$");
            if (cast.Success)
            {
                text = text[..cast.Index].Trim();
                changed = true;
            }
        }

        text = Regex.Replace(text, @"\s+", " ");
        return text.ToLowerInvariant();
    }

    private static bool IsBalanced(string text)
    {
        var depth = 0;
        foreach (var character in text)
        {
            if (character == '(') depth++;
            else if (character == ')') depth--;
            if (depth < 0) return false;
        }
        return depth == 0;
    }

    private static void Fail(List<SchemaIssue> issues, string code, string objectName, string expected, string actual)
    {
        if (issues.Any(issue => issue.Code == code && issue.Object == objectName))
            return;
        issues.Add(new SchemaIssue(code, objectName, expected, actual));
    }

    private static IReadOnlyList<string> ReadSourceMigrationIds() =>
        (from type in typeof(PostgresDbContext).Assembly.GetTypes()
         let attribute = type.GetCustomAttribute<MigrationAttribute>()
         where attribute is not null
         select attribute.Id)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    private static ModelContract BuildModelContract()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(MetadataOnlyConnection)
            .Options;
        using var context = new PostgresDbContext(options);
        var tables = new Dictionary<(string Schema, string Table), ModelTableBuilder>();

        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            var schema = entity.GetSchema();
            if (table is null)
            {
                var owner = entity.FindOwnership()?.PrincipalEntityType;
                table = owner?.GetTableName();
                schema ??= owner?.GetSchema();
            }

            if (table is null)
                continue;

            schema ??= PostgresSchemas.App;
            if (!tables.TryGetValue((schema, table), out var builder))
            {
                builder = new ModelTableBuilder(schema, table);
                tables[(schema, table)] = builder;
            }

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName();
                if (column is null)
                    continue;

                // GetDefaultValue() returns the CLR default for every non-nullable property, so a
                // database default must be read from the relational annotation instead.
                var defaultSql = property.GetDefaultValueSql();
                if (defaultSql is null &&
                    property.FindAnnotation(RelationalAnnotationNames.DefaultValue)?.Value is { } configuredDefault)
                    defaultSql = RenderDefault(configuredDefault);

                builder.Columns[column] = new ModelColumn(
                    column,
                    property.GetColumnType(),
                    property.IsColumnNullable(),
                    defaultSql);
            }

            if (entity.IsOwned())
                continue;

            var primaryKey = entity.FindPrimaryKey();
            if (primaryKey is not null)
                builder.PrimaryKey = primaryKey.Properties
                    .Select(property => property.GetColumnName() ?? property.Name)
                    .ToArray();

            foreach (var key in entity.GetKeys().Where(key => !key.IsPrimaryKey()))
                builder.UniqueKeys.Add(key.Properties.Select(property => property.GetColumnName() ?? property.Name).ToArray());

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var principal = foreignKey.PrincipalEntityType;
                builder.ForeignKeys.Add(new ModelForeignKey(
                    foreignKey.Properties.Select(property => property.GetColumnName() ?? property.Name).ToArray(),
                    principal.GetSchema() ?? PostgresSchemas.App,
                    principal.GetTableName() ?? principal.Name,
                    foreignKey.PrincipalKey.Properties.Select(property => property.GetColumnName() ?? property.Name).ToArray()));
            }

            foreach (var index in entity.GetIndexes())
            {
                var columns = index.Properties.Select(property => property.GetColumnName() ?? property.Name).ToArray();
                if (columns.SequenceEqual(builder.PrimaryKey, StringComparer.Ordinal) && index.IsUnique)
                    continue;
                builder.Indexes.Add(new ModelIndex(columns, index.IsUnique, index.GetFilter()));
            }
        }

        return new ModelContract(tables.ToDictionary(
            entry => entry.Key,
            entry => new ModelTable(entry.Value.Schema, entry.Value.Table, entry.Value.Columns,
                entry.Value.PrimaryKey, entry.Value.UniqueKeys, entry.Value.ForeignKeys, entry.Value.Indexes),
            EqualityComparer<(string Schema, string Table)>.Default));
    }

    private static string? RenderDefault(object? value)
    {
        return value switch
        {
            null => null,
            bool boolean => boolean ? "true" : "false",
            string text => $"'{text.Replace("'", "''")}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private sealed class ModelTableBuilder(string schema, string table)
    {
        public string Schema { get; } = schema;
        public string Table { get; } = table;
        public Dictionary<string, ModelColumn> Columns { get; } = new(StringComparer.Ordinal);
        public string[] PrimaryKey { get; set; } = [];
        public List<string[]> UniqueKeys { get; } = [];
        public List<ModelForeignKey> ForeignKeys { get; } = [];
        public List<ModelIndex> Indexes { get; } = [];
    }

    private sealed record ModelContract(IReadOnlyDictionary<(string Schema, string Table), ModelTable> Tables);
    private sealed record ModelTable(
        string Schema,
        string Table,
        IReadOnlyDictionary<string, ModelColumn> Columns,
        IReadOnlyList<string> PrimaryKey,
        IReadOnlyList<string[]> UniqueKeys,
        IReadOnlyList<ModelForeignKey> ForeignKeys,
        IReadOnlyList<ModelIndex> Indexes);
    private sealed record ModelColumn(string Name, string DataType, bool Nullable, string? DefaultSql);
    private sealed record ModelForeignKey(string[] Columns, string ReferencedSchema, string ReferencedTable, string[] ReferencedColumns);
    private sealed record ModelIndex(string[] Columns, bool Unique, string? Predicate);
}

/// <summary>
/// One read-only catalog snapshot. Only schema metadata is read; no application rows are selected.
/// </summary>
internal sealed class CatalogSnapshot
{
    public required string DatabaseName { get; init; }
    public required string ServerVersion { get; init; }
    public required int ServerVersionNumber { get; init; }
    public required HashSet<string> Schemas { get; init; }
    public required Dictionary<(string Schema, string Table), PhysicalTable> Tables { get; init; }
    public required Dictionary<string, PhysicalRole> Roles { get; init; }
    public required List<PhysicalFunction> Functions { get; init; }
    public required List<string> AclLines { get; init; }
    public required List<string> AppliedMigrationIds { get; init; }
    public string FingerprintSha256 { get; internal set; } = string.Empty;

    public bool? HasPrivilege(SchemaPrivilegeRequirement requirement, string privilege)
    {
        if (!Roles.ContainsKey(requirement.Role))
            return null;

        var schema = requirement.Schema;
        var target = requirement.Object;
        var sql = requirement.ObjectType switch
        {
            "schema" =>
                "SELECT has_schema_privilege(role_name, schema_name, privilege_name)",
            "function" =>
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_proc AS p
                    JOIN pg_namespace AS n ON n.oid = p.pronamespace
                    WHERE n.nspname = @schema AND p.proname = @target
                      AND (@arguments = '' OR pg_get_function_identity_arguments(p.oid) = @arguments)
                      AND has_function_privilege(@role, p.oid, @privilege))
                """,
            "table" when requirement.Columns.Count > 0 =>
                """
                SELECT bool_and(has_column_privilege(@role, format('%I.%I', @schema::text, @target::text), column_name, @privilege))
                FROM unnest(@columns::text[]) AS requested(column_name)
                """,
            _ =>
                "SELECT has_table_privilege(@role, format('%I.%I', @schema::text, @target::text), @privilege)"
        };

        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        command.Parameters.AddWithValue("role", requirement.Role);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("target", target);
        command.Parameters.AddWithValue("privilege", privilege);
        if (requirement.ObjectType == "function")
            command.Parameters.AddWithValue("arguments", requirement.Arguments);
        if (requirement.Columns.Count > 0)
        {
            var columns = command.Parameters.Add("columns", NpgsqlDbType.Array | NpgsqlDbType.Text);
            columns.Value = requirement.Columns.ToArray();
        }

        try
        {
            var value = command.ExecuteScalar();
            return value is bool boolean ? boolean : null;
        }
        catch (PostgresException)
        {
            return null;
        }
    }

    internal string ConnectionString { get; set; } = string.Empty;

    public static async Task<CatalogSnapshot> CollectAsync(
        string connectionString,
        SchemaRequirements requirements,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "SET TRANSACTION READ ONLY", cancellationToken);
        await ExecuteAsync(connection, transaction,
            $"SET LOCAL statement_timeout = '{DatabaseSchemaCompatibilitySettings.StatementTimeout}'", cancellationToken);
        await ExecuteAsync(connection, transaction,
            $"SET LOCAL lock_timeout = '{DatabaseSchemaCompatibilitySettings.LockTimeout}'", cancellationToken);
        await ExecuteAsync(connection, transaction,
            $"SET LOCAL idle_in_transaction_session_timeout = '{DatabaseSchemaCompatibilitySettings.IdleTransactionTimeout}'",
            cancellationToken);

        var identity = await ReadIdentityAsync(connection, transaction, cancellationToken);
        var schemas = await ReadSchemasAsync(connection, transaction, cancellationToken);
        var tables = await ReadTablesAsync(connection, transaction, cancellationToken);
        var columns = await ReadColumnsAsync(connection, transaction, cancellationToken);
        var constraints = await ReadConstraintsAsync(connection, transaction, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, transaction, cancellationToken);
        var policies = await ReadPoliciesAsync(connection, transaction, cancellationToken);
        var functions = await ReadFunctionsAsync(connection, transaction, cancellationToken);
        var roleNames = requirements.Roles.Select(role => role.Name).ToArray();
        var roles = await ReadRolesAsync(connection, transaction, roleNames, cancellationToken);
        var aclLines = await ReadAclsAsync(connection, transaction, cancellationToken);
        var applied = await ReadMigrationHistoryAsync(connection, transaction,
            requirements.MigrationHistory.Schema, requirements.MigrationHistory.Table, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var column in columns)
            if (tables.TryGetValue((column.Schema, column.Table), out var table))
                table.Columns[column.Name] = column;

        foreach (var constraint in constraints)
            if (tables.TryGetValue((constraint.Schema, constraint.Table), out var table))
                table.Constraints[constraint.Name] = constraint;

        foreach (var index in indexes)
            if (tables.TryGetValue((index.Schema, index.Table), out var table))
                table.Indexes[index.Name] = index;

        foreach (var policy in policies)
            if (tables.TryGetValue((policy.Schema, policy.Table), out var table))
                table.Policies[policy.Name] = policy;

        var snapshot = new CatalogSnapshot
        {
            DatabaseName = identity.DatabaseName,
            ServerVersion = identity.ServerVersion,
            ServerVersionNumber = identity.ServerVersionNumber,
            Schemas = schemas,
            Tables = tables,
            Roles = roles,
            Functions = functions,
            AclLines = aclLines,
            AppliedMigrationIds = applied,
            ConnectionString = connectionString
        };
        snapshot.FingerprintSha256 = ComputeFingerprint(snapshot, roles);
        return snapshot;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string DatabaseName, string ServerVersion, int ServerVersionNumber)> ReadIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT current_database(), current_setting('server_version'), current_setting('server_version_num')::integer
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
    }

    private static async Task<HashSet<string>> ReadSchemasAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT nspname::text FROM pg_namespace WHERE nspname::text = ANY(@schemas)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var schemas = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            schemas.Add(reader.GetString(0));
        return schemas;
    }

    private static async Task<Dictionary<(string Schema, string Table), PhysicalTable>> ReadTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, c.relname::text, pg_get_userbyid(c.relowner)::text,
                   c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p') AND n.nspname::text = ANY(@schemas)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var tables = new Dictionary<(string Schema, string Table), PhysicalTable>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = new PhysicalTable
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Owner = reader.GetString(2),
                RowLevelSecurity = reader.GetBoolean(3),
                ForceRowLevelSecurity = reader.GetBoolean(4)
            };
            tables[(table.Schema, table.Name)] = table;
        }
        return tables;
    }

    private static async Task<List<PhysicalColumn>> ReadColumnsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, c.relname::text, a.attname::text,
                   format_type(a.atttypid, a.atttypmod),
                   a.attnotnull,
                   pg_get_expr(d.adbin, d.adrelid)
            FROM pg_attribute AS a
            JOIN pg_class AS c ON c.oid = a.attrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef AS d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE c.relkind IN ('r', 'p') AND n.nspname::text = ANY(@schemas)
              AND a.attnum > 0 AND NOT a.attisdropped
            ORDER BY n.nspname::text, c.relname::text, a.attnum
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var columns = new List<PhysicalColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(new PhysicalColumn
            {
                Schema = reader.GetString(0),
                Table = reader.GetString(1),
                Name = reader.GetString(2),
                DataType = reader.GetString(3),
                Nullable = !reader.GetBoolean(4),
                DefaultSql = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        return columns;
    }

    private static async Task<List<PhysicalConstraint>> ReadConstraintsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, c.relname::text, con.conname::text, con.contype::text,
                   pg_get_constraintdef(con.oid, true),
                   COALESCE((SELECT array_agg(att.attname::text ORDER BY k.ord)
                             FROM unnest(con.conkey) WITH ORDINALITY AS k(attnum, ord)
                             JOIN pg_attribute AS att ON att.attrelid = con.conrelid AND att.attnum = k.attnum),
                            ARRAY[]::text[]),
                   COALESCE((SELECT array_agg(att.attname::text ORDER BY k.ord)
                             FROM unnest(con.confkey) WITH ORDINALITY AS k(attnum, ord)
                             JOIN pg_attribute AS att ON att.attrelid = con.confrelid AND att.attnum = k.attnum),
                            ARRAY[]::text[]),
                   fn.nspname::text, fc.relname::text
            FROM pg_constraint AS con
            JOIN pg_class AS c ON c.oid = con.conrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            LEFT JOIN pg_class AS fc ON fc.oid = con.confrelid
            LEFT JOIN pg_namespace AS fn ON fn.oid = fc.relnamespace
            WHERE n.nspname::text = ANY(@schemas) AND con.contype IN ('p', 'u', 'f', 'c', 'x')
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var constraints = new List<PhysicalConstraint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            constraints.Add(new PhysicalConstraint
            {
                Schema = reader.GetString(0),
                Table = reader.GetString(1),
                Name = reader.GetString(2),
                Type = reader.GetString(3),
                Definition = reader.GetString(4),
                Columns = reader.GetFieldValue<string[]>(5),
                ReferencedColumns = reader.GetFieldValue<string[]>(6),
                ReferencedSchema = reader.IsDBNull(7) ? null : reader.GetString(7),
                ReferencedTable = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        return constraints;
    }

    private static async Task<List<PhysicalIndex>> ReadIndexesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, c.relname::text, ic.relname::text,
                   i.indisunique, i.indisvalid, i.indisready,
                   pg_get_expr(i.indpred, i.indrelid),
                   COALESCE((SELECT array_agg(pg_get_indexdef(i.indexrelid, k.ord, true) ORDER BY k.ord)
                             FROM generate_series(1, i.indnkeyatts) AS k(ord)), ARRAY[]::text[])
            FROM pg_index AS i
            JOIN pg_class AS ic ON ic.oid = i.indexrelid
            JOIN pg_class AS c ON c.oid = i.indrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p') AND n.nspname::text = ANY(@schemas)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var indexes = new List<PhysicalIndex>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            indexes.Add(new PhysicalIndex
            {
                Schema = reader.GetString(0),
                Table = reader.GetString(1),
                Name = reader.GetString(2),
                Unique = reader.GetBoolean(3),
                Valid = reader.GetBoolean(4),
                Ready = reader.GetBoolean(5),
                Predicate = reader.IsDBNull(6) ? null : reader.GetString(6),
                Columns = reader.GetFieldValue<string[]>(7)
            });
        return indexes;
    }

    private static async Task<List<PhysicalPolicy>> ReadPoliciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, c.relname::text, p.polname::text,
                   CASE p.polcmd WHEN '*' THEN 'ALL' WHEN 'r' THEN 'SELECT' WHEN 'a' THEN 'INSERT'
                                 WHEN 'w' THEN 'UPDATE' WHEN 'd' THEN 'DELETE' ELSE p.polcmd::text END,
                   p.polpermissive,
                   COALESCE(pg_get_expr(p.polqual, p.polrelid), ''),
                   COALESCE(pg_get_expr(p.polwithcheck, p.polrelid), ''),
                   COALESCE((SELECT array_agg(CASE WHEN role_oid = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(role_oid)::text END
                                              ORDER BY role_oid)
                             FROM unnest(p.polroles) AS role_oid), ARRAY[]::text[])
            FROM pg_policy AS p
            JOIN pg_class AS c ON c.oid = p.polrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname::text = ANY(@schemas)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var policies = new List<PhysicalPolicy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            policies.Add(new PhysicalPolicy
            {
                Schema = reader.GetString(0),
                Table = reader.GetString(1),
                Name = reader.GetString(2),
                Command = reader.GetString(3),
                Permissive = reader.GetBoolean(4),
                Predicate = reader.GetString(5),
                Check = reader.GetString(6),
                Roles = reader.GetFieldValue<string[]>(7)
            });
        return policies;
    }

    private static async Task<List<PhysicalFunction>> ReadFunctionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.nspname::text, p.proname::text, pg_get_function_identity_arguments(p.oid),
                   p.prosecdef, pg_get_userbyid(p.proowner)::text, p.proconfig,
                   COALESCE((SELECT array_agg(
                                (CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee)::text END)
                                || ':' || a.privilege_type
                                ORDER BY (CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee)::text END)
                                         || ':' || a.privilege_type)
                             FROM aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) AS a),
                            ARRAY[]::text[])
            FROM pg_proc AS p
            JOIN pg_namespace AS n ON n.oid = p.pronamespace
            WHERE n.nspname::text = ANY(@schemas)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
        AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
        var functions = new List<PhysicalFunction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            functions.Add(new PhysicalFunction
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Arguments = reader.GetString(2),
                SecurityDefiner = reader.GetBoolean(3),
                Owner = reader.GetString(4),
                SearchPath = reader.IsDBNull(5) ? [] : reader.GetFieldValue<string[]>(5),
                Grants = reader.GetFieldValue<string[]>(6)
            });
        return functions;
    }

    private static async Task<Dictionary<string, PhysicalRole>> ReadRolesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string[] roleNames,
        CancellationToken cancellationToken)
    {
        var roles = new Dictionary<string, PhysicalRole>(StringComparer.Ordinal);
        if (roleNames.Length == 0)
            return roles;

        const string roleSql = """
            SELECT rolname::text, rolcanlogin, rolsuper, rolbypassrls, rolcreatedb, rolcreaterole
            FROM pg_roles WHERE rolname::text = ANY(@roles)
            """;
        await using (var command = new NpgsqlCommand(roleSql, connection, transaction))
        {
            command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
            AddTextArray(command, "roles", roleNames);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var role = new PhysicalRole
                {
                    Name = reader.GetString(0),
                    Login = reader.GetBoolean(1),
                    Superuser = reader.GetBoolean(2),
                    BypassRls = reader.GetBoolean(3),
                    CreateDb = reader.GetBoolean(4),
                    CreateRole = reader.GetBoolean(5)
                };
                roles[role.Name] = role;
            }
        }

        const string memberSql = """
            SELECT member.rolname::text, granted.rolname::text
            FROM pg_auth_members AS m
            JOIN pg_roles AS member ON member.oid = m.member
            JOIN pg_roles AS granted ON granted.oid = m.roleid
            WHERE member.rolname::text = ANY(@roles)
            """;
        await using (var command = new NpgsqlCommand(memberSql, connection, transaction))
        {
            command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
            AddTextArray(command, "roles", roleNames);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                if (roles.TryGetValue(reader.GetString(0), out var role))
                    role.Memberships.Add(reader.GetString(1));
        }

        return roles;
    }

    private static async Task<List<string>> ReadAclsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await ReadAclQueryAsync("schema", """
            SELECT 'schema', n.nspname::text, '',
                   CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee)::text END,
                   a.privilege_type, a.is_grantable
            FROM pg_namespace AS n
            CROSS JOIN LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) AS a
            WHERE n.nspname::text = ANY(@schemas)
            """);
        await ReadAclQueryAsync("table", """
            SELECT 'table', n.nspname::text, c.relname::text,
                   CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee)::text END,
                   a.privilege_type, a.is_grantable
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) AS a
            WHERE c.relkind IN ('r', 'p') AND n.nspname::text = ANY(@schemas)
            """);
        await ReadAclQueryAsync("column", """
            SELECT 'column', n.nspname::text || '.' || c.relname::text, att.attname::text,
                   CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee)::text END,
                   a.privilege_type, a.is_grantable
            FROM pg_attribute AS att
            JOIN pg_class AS c ON c.oid = att.attrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(att.attacl) AS a
            WHERE att.attacl IS NOT NULL AND att.attnum > 0 AND NOT att.attisdropped
              AND c.relkind IN ('r', 'p') AND n.nspname::text = ANY(@schemas)
            """);
        return lines;

        async Task ReadAclQueryAsync(string kind, string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
            AddTextArray(command, "schemas", DatabaseSchemaCompatibilityInspectedSchemas);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                lines.Add(string.Join('|', kind, reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetBoolean(5) ? "grantable" : "no-grant"));
        }
    }

    private static async Task<List<string>> ReadMigrationHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string historySchema,
        string historyTable,
        CancellationToken cancellationToken)
    {
        var qualified = $"{QuoteIdentifier(historySchema)}.{QuoteIdentifier(historyTable)}";
        var existsSql = "SELECT to_regclass(@qualified) IS NOT NULL";
        await using (var command = new NpgsqlCommand(existsSql, connection, transaction))
        {
            command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
            command.Parameters.AddWithValue("qualified", qualified);
            var exists = await command.ExecuteScalarAsync(cancellationToken);
            if (exists is not true)
                return [];
        }

        var sql = $"SELECT \"MigrationId\" FROM {qualified} ORDER BY \"MigrationId\"";
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.CommandTimeout = DatabaseSchemaCompatibilitySettings.CommandTimeoutSeconds;
            var ids = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetString(0));
            return ids;
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static void AddTextArray(NpgsqlCommand command, string name, IEnumerable<string> values)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Array | NpgsqlDbType.Text);
        parameter.Value = values.ToArray();
    }

    private static string ComputeFingerprint(CatalogSnapshot snapshot, IReadOnlyDictionary<string, PhysicalRole> roles)
    {
        var builder = new StringBuilder();
        Append(builder, "database", snapshot.DatabaseName);
        Append(builder, "server", snapshot.ServerVersionNumber.ToString(CultureInfo.InvariantCulture));

        foreach (var schema in snapshot.Schemas.Order(StringComparer.Ordinal))
            Append(builder, "schema", schema);

        foreach (var table in snapshot.Tables.Values
                     .OrderBy(table => table.Schema, StringComparer.Ordinal)
                     .ThenBy(table => table.Name, StringComparer.Ordinal))
        {
            Append(builder, "table", $"{table.Schema}.{table.Name}");
            Append(builder, "rls", $"{table.RowLevelSecurity}/{table.ForceRowLevelSecurity}");
            Append(builder, "owner", table.Owner);
            foreach (var column in table.Columns.Values.OrderBy(column => column.Name, StringComparer.Ordinal))
            {
                Append(builder, "column", $"{table.Schema}.{table.Name}.{column.Name}");
                Append(builder, "type", column.DataType);
                Append(builder, "null", column.Nullable ? "1" : "0");
                Append(builder, "default", column.DefaultSql ?? string.Empty);
            }

            foreach (var constraint in table.Constraints.Values.OrderBy(constraint => constraint.Name, StringComparer.Ordinal))
            {
                Append(builder, "constraint", $"{table.Schema}.{table.Name}.{constraint.Name}");
                Append(builder, "definition", constraint.Definition);
            }

            foreach (var index in table.Indexes.Values.OrderBy(index => index.Name, StringComparer.Ordinal))
            {
                Append(builder, "index", $"{table.Schema}.{table.Name}.{index.Name}");
                Append(builder, "keys", string.Join(",", index.Columns));
                Append(builder, "flags", $"{index.Unique}/{index.Valid}/{index.Ready}");
                Append(builder, "predicate", index.Predicate ?? string.Empty);
            }

            foreach (var policy in table.Policies.Values.OrderBy(policy => policy.Name, StringComparer.Ordinal))
            {
                Append(builder, "policy", $"{table.Schema}.{table.Name}.{policy.Name}");
                Append(builder, "command", $"{policy.Command}/{policy.Permissive}");
                Append(builder, "roles", string.Join(",", policy.Roles));
                Append(builder, "expressions", $"{policy.Predicate}|{policy.Check}");
            }
        }

        foreach (var function in snapshot.Functions
                     .OrderBy(function => function.Schema, StringComparer.Ordinal)
                     .ThenBy(function => function.Name, StringComparer.Ordinal)
                     .ThenBy(function => function.Arguments, StringComparer.Ordinal))
        {
            Append(builder, "function", $"{function.Schema}.{function.Name}({function.Arguments})");
            Append(builder, "security", $"{function.SecurityDefiner}/{function.Owner}");
            Append(builder, "search_path", string.Join(",", function.SearchPath));
            Append(builder, "grants", string.Join(",", function.Grants.Order(StringComparer.Ordinal)));
        }

        foreach (var acl in snapshot.AclLines.Order(StringComparer.Ordinal))
            Append(builder, "acl", acl);

        foreach (var role in roles.Values.OrderBy(role => role.Name, StringComparer.Ordinal))
        {
            Append(builder, "role", role.Name);
            Append(builder, "attributes",
                $"{role.Login}/{role.Superuser}/{role.BypassRls}/{role.CreateDb}/{role.CreateRole}");
            Append(builder, "memberships", string.Join(",", role.Memberships.Order(StringComparer.Ordinal)));
        }

        foreach (var id in snapshot.AppliedMigrationIds)
            Append(builder, "migration", id);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append(name).Append('=').Append(value).Append('\n');

    private static readonly string[] DatabaseSchemaCompatibilityInspectedSchemas =
        [PostgresSchemas.App, PostgresSchemas.Catalog, PostgresSchemas.Operations];
}

internal static class DatabaseSchemaCompatibilitySettings
{
    public const int CommandTimeoutSeconds = 60;
    public const string LockTimeout = "5s";
    public const string StatementTimeout = "60s";
    public const string IdleTransactionTimeout = "60s";
}

internal sealed class PhysicalTable
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string Owner { get; init; }
    public bool RowLevelSecurity { get; init; }
    public bool ForceRowLevelSecurity { get; init; }
    public Dictionary<string, PhysicalColumn> Columns { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PhysicalConstraint> Constraints { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PhysicalIndex> Indexes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PhysicalPolicy> Policies { get; } = new(StringComparer.Ordinal);
}

internal sealed class PhysicalColumn
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool Nullable { get; init; }
    public string? DefaultSql { get; init; }
}

internal sealed class PhysicalConstraint
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Definition { get; init; }
    public required string[] Columns { get; init; }
    public required string[] ReferencedColumns { get; init; }
    public string? ReferencedSchema { get; init; }
    public string? ReferencedTable { get; init; }
}

internal sealed class PhysicalIndex
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public required string Name { get; init; }
    public bool Unique { get; init; }
    public bool Valid { get; init; }
    public bool Ready { get; init; }
    public string? Predicate { get; init; }
    public required string[] Columns { get; init; }
}

internal sealed class PhysicalPolicy
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }
    public bool Permissive { get; init; }
    public required string Predicate { get; init; }
    public required string Check { get; init; }
    public required string[] Roles { get; init; }
}

internal sealed class PhysicalFunction
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
    public bool SecurityDefiner { get; init; }
    public required string Owner { get; init; }
    public required string[] SearchPath { get; init; }
    public required string[] Grants { get; init; }
}

internal sealed class PhysicalRole
{
    public required string Name { get; init; }
    public bool Login { get; init; }
    public bool Superuser { get; init; }
    public bool BypassRls { get; init; }
    public bool CreateDb { get; init; }
    public bool CreateRole { get; init; }
    public HashSet<string> Memberships { get; } = new(StringComparer.Ordinal);
}
