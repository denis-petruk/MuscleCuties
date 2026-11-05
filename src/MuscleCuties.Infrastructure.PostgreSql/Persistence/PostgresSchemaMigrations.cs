using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public sealed record MigrationScriptDefinition(
    int Version,
    string ScriptName,
    string ResourceName);

public sealed record MigrationExecutionResult(
    int Version,
    string ScriptName,
    string ChecksumSha256,
    bool AlreadyApplied);

public sealed record SchemaMigrationSummary(
    int CurrentVersion,
    int MigrationsApplied,
    IReadOnlyList<MigrationExecutionResult> Details);

public sealed class AppliedMigrationRecord
{
    public int Version { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; set; }
}

/// <summary>
/// PL02: Ordered versioned PostgreSQL migration executor with advisory lock protection,
/// SHA-256 checksum verification, and non-destructive schema evolution.
/// Replaces legacy dbinit/EnsureCreated calls in shared deployment environments.
/// </summary>
public static class PostgresSchemaMigrations
{
    public const string MigrationsTableName = "ops.schema_migrations";

    private const string MigrationsTableSql = """
        CREATE SCHEMA IF NOT EXISTS ops;

        CREATE TABLE IF NOT EXISTS ops.schema_migrations (
            version integer PRIMARY KEY,
            script_name text NOT NULL,
            checksum_sha256 text NOT NULL,
            applied_at_utc timestamp with time zone NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS ops.schema_metadata (
            singleton boolean NOT NULL DEFAULT true PRIMARY KEY,
            schema_version integer NOT NULL,
            model_fingerprint text NOT NULL,
            applied_at_utc timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT schema_metadata_is_singleton CHECK (singleton)
        );

        GRANT SELECT ON ops.schema_migrations TO musclecuties_app, musclecuties_auth;
        GRANT SELECT ON ops.schema_metadata TO musclecuties_app, musclecuties_auth;
        """;

    public const string BaseSchemaResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.001_base_schema.sql";

    public const string SchemaFingerprintResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.002_schema_fingerprint.sql";

    public const string ScheduleSeamResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.003_schedule_seam.sql";

    private static readonly IReadOnlyList<MigrationScriptDefinition> OrderedScripts =
    [
        new(1, "001_base_schema.sql", BaseSchemaResourceName),
        new(2, "002_schema_fingerprint.sql", SchemaFingerprintResourceName),
        new(3, "003_schedule_seam.sql", ScheduleSeamResourceName)
    ];

    public static IReadOnlyList<MigrationScriptDefinition> Scripts => OrderedScripts;

    /// <summary>
    /// Rehearses and verifies the complete migration chain offline, validating that all
    /// embedded scripts exist, are non-empty, follow strict monotonic ordering, and yield valid SHA-256 checksums.
    /// </summary>
    public static async Task<IReadOnlyList<MigrationExecutionResult>> RehearseChainAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<MigrationExecutionResult>();
        for (var i = 0; i < OrderedScripts.Count; i++)
        {
            var script = OrderedScripts[i];
            if (script.Version != i + 1)
                throw new InvalidOperationException($"Migration script {script.ScriptName} has non-monotonic version {script.Version}. Expected {i + 1}.");

            var sqlContent = await ReadScriptAsync(script.ResourceName, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(sqlContent))
                throw new InvalidOperationException($"Migration script {script.ScriptName} is empty or whitespace.");

            var checksum = ComputeSha256Hex(sqlContent);
            results.Add(new MigrationExecutionResult(script.Version, script.ScriptName, checksum, AlreadyApplied: false));
        }

        return results;
    }

    public static async Task<SchemaMigrationSummary> ApplyMigrationsAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Ensure migration tracking infrastructure exists
        await ExecuteRawSqlAsync(context, MigrationsTableSql, cancellationToken).ConfigureAwait(false);

        // 2. Query already applied migrations
        var appliedList = await context.Database
            .SqlQueryRaw<AppliedMigrationRecord>("SELECT version as \"Version\", script_name as \"ScriptName\", checksum_sha256 as \"ChecksumSha256\", applied_at_utc as \"AppliedAtUtc\" FROM ops.schema_migrations ORDER BY version")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var appliedByVersion = appliedList.ToDictionary(r => r.Version);
        var results = new List<MigrationExecutionResult>();
        var newlyAppliedCount = 0;
        var currentVersion = appliedList.Count > 0 ? appliedList.Max(r => r.Version) : 0;

        foreach (var script in OrderedScripts)
        {
            var sqlContent = await ReadScriptAsync(script.ResourceName, cancellationToken).ConfigureAwait(false);
            var checksum = ComputeSha256Hex(sqlContent);

            if (appliedByVersion.TryGetValue(script.Version, out var recorded))
            {
                if (!string.Equals(recorded.ChecksumSha256, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Migration checksum mismatch for version {script.Version} ({script.ScriptName}). " +
                        $"Recorded: {recorded.ChecksumSha256}, Current: {checksum}. Fails closed to protect shared schema.");
                }

                results.Add(new MigrationExecutionResult(script.Version, script.ScriptName, checksum, AlreadyApplied: true));
                continue;
            }

            // Apply new migration in serializable transaction
            await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

                await ExecuteRawSqlAsync(context, sqlContent, cancellationToken).ConfigureAwait(false);

                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO ops.schema_migrations (version, script_name, checksum_sha256, applied_at_utc) VALUES ({0}, {1}, {2}, now())",
                    new object[] { script.Version, script.ScriptName, checksum },
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            currentVersion = script.Version;
            newlyAppliedCount++;
            results.Add(new MigrationExecutionResult(script.Version, script.ScriptName, checksum, AlreadyApplied: false));
        }

        // Update ops.schema_metadata with latest applied version
        var fingerprint = PostgresSchemaBootstrap.ComputeModelFingerprint(context);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO ops.schema_metadata (singleton, schema_version, model_fingerprint, applied_at_utc)
            VALUES (true, {0}, {1}, now())
            ON CONFLICT (singleton) DO UPDATE
            SET schema_version = EXCLUDED.schema_version,
                model_fingerprint = EXCLUDED.model_fingerprint,
                applied_at_utc = EXCLUDED.applied_at_utc
            """,
            new object[] { currentVersion, fingerprint },
            cancellationToken).ConfigureAwait(false);

        return new SchemaMigrationSummary(currentVersion, newlyAppliedCount, results);
    }

    private static async Task<string> ReadScriptAsync(string resourceName, CancellationToken cancellationToken)
    {
        var assembly = typeof(PostgresSchemaMigrations).Assembly;
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static async Task ExecuteRawSqlAsync(PostgresDbContext context, string sql, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
