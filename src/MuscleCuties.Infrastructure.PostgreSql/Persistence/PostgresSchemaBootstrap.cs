using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public sealed record SchemaBootstrapResult(
    bool CreatedFromModel,
    int SecurityStatementCount,
    string ModelFingerprint,
    CanonicalCatalogSeedSummary? Seed);

/// <summary>
/// Model-first schema creation. There is no migration chain: the EF model is the single source of
/// table, column, index and constraint truth, and <c>security.sql</c> owns the objects the model cannot
/// express (tenant policies, service roles and grants, SECURITY DEFINER functions).
/// Both steps are idempotent so repeated deploys converge instead of drifting.
/// </summary>
public static class PostgresSchemaBootstrap
{
    public const string SecurityScriptResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.security.sql";

    public const string SchemaSupplementResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.schema_supplement.sql";

    public const string MetadataTableName = "ops.schema_metadata";

    private const int CurrentSchemaVersion = 1;
    private const char StatementSeparator = ';';

    private const string MetadataTableSql = """
        CREATE SCHEMA IF NOT EXISTS ops;

        CREATE TABLE IF NOT EXISTS ops.schema_metadata (
            singleton boolean NOT NULL DEFAULT true PRIMARY KEY,
            schema_version integer NOT NULL,
            model_fingerprint text NOT NULL,
            applied_at_utc timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT schema_metadata_is_singleton CHECK (singleton)
        );

        GRANT SELECT ON ops.schema_metadata TO musclecuties_app, musclecuties_auth;
        """;

    private const string RecordMetadataSql = """
        INSERT INTO ops.schema_metadata (singleton, schema_version, model_fingerprint, applied_at_utc)
        VALUES (true, @schema_version, @model_fingerprint, now())
        ON CONFLICT (singleton) DO UPDATE
        SET schema_version = EXCLUDED.schema_version,
            model_fingerprint = EXCLUDED.model_fingerprint,
            applied_at_utc = EXCLUDED.applied_at_utc;
        """;

    public static async Task<SchemaBootstrapResult> InitializeAsync(
        PostgresDbContext context,
        bool seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var createdFromModel = await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        var supplementalSql = await ReadEmbeddedScriptAsync(SchemaSupplementResourceName, cancellationToken)
            .ConfigureAwait(false);
        await ExecuteScriptAsync(context, supplementalSql, cancellationToken).ConfigureAwait(false);

        var securitySql = await ReadSecurityScriptAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteScriptAsync(context, securitySql, cancellationToken).ConfigureAwait(false);
        await ExecuteScriptAsync(context, MetadataTableSql, cancellationToken).ConfigureAwait(false);

        CanonicalCatalogSeedSummary? seedSummary = null;
        if (seed)
        {
            seedSummary = await PostgresCanonicalCatalogSeeder
                .IngestEmbeddedManifestAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }

        var fingerprint = ComputeModelFingerprint(context);
        await context.Database.ExecuteSqlRawAsync(
            RecordMetadataSql,
            [
                new NpgsqlParameter("schema_version", CurrentSchemaVersion),
                new NpgsqlParameter("model_fingerprint", fingerprint)
            ]).ConfigureAwait(false);

        return new SchemaBootstrapResult(
            createdFromModel,
            CountStatements(securitySql),
            fingerprint,
            seedSummary);
    }

    public static Task<string> ReadSecurityScriptAsync(CancellationToken cancellationToken = default) =>
        ReadEmbeddedScriptAsync(SecurityScriptResourceName, cancellationToken);

    /// <summary>
    /// Runs a multi-statement script through the provider directly. EF's raw SQL helpers treat the text as
    /// a composite format string, which corrupts scripts that contain braces or parameter-like tokens.
    /// </summary>
    private static async Task ExecuteScriptAsync(
        PostgresDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = PostgreSqlTimeoutDefaults.CommandSeconds;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (mustClose)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadEmbeddedScriptAsync(
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var stream = typeof(PostgresSchemaBootstrap).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded database script '{resourceName}' is unavailable.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stable digest of every table and column in the model. Readiness and <c>dbcheck</c> compare it with
    /// the value recorded at bootstrap so a deployed assembly cannot silently serve a stale schema.
    /// </summary>
    public static string ComputeModelFingerprint(PostgresDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tables = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
                continue;

            var schema = entityType.GetSchema() ?? entityType.GetDefaultSchema();
            var qualifiedName = string.IsNullOrEmpty(schema) ? tableName : $"{schema}.{tableName}";

            if (!tables.TryGetValue(qualifiedName, out var columns))
            {
                columns = new SortedSet<string>(StringComparer.Ordinal);
                tables[qualifiedName] = columns;
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrEmpty(columnName))
                    columns.Add(columnName);
            }
        }

        var builder = new StringBuilder();
        foreach (var (table, columns) in tables)
        {
            builder.Append(table).Append(':').AppendJoin(',', columns).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static int CountStatements(string sql) =>
        sql.Split(StatementSeparator).Length - 1;
}
