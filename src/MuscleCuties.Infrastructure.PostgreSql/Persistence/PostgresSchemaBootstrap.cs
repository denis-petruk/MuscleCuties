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
/// Disposable/test-only model-first initialization. Production environments must use
/// <c>migrate</c> (frozen baseline plus versioned evolution) followed by <c>seed</c>.
/// This path exists for loopback test databases and explicitly disposable CI targets; it
/// creates the model, applies the same versioned ledger, then seeds the canonical catalog.
/// </summary>
public static class PostgresSchemaBootstrap
{
    public const string SecurityScriptResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.security.sql";

    public const string SchemaSupplementResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.schema_supplement.sql";

    public const string MetadataTableName = "ops.schema_metadata";

    private const char StatementSeparator = ';';

    public static bool IsDisposableTarget(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return true;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = builder.Host;
            return host is "127.0.0.1" or "localhost" or "::1" ||
                   string.Equals(Environment.GetEnvironmentVariable("MC_ALLOW_ENSURE_CREATED"), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<SchemaBootstrapResult> InitializeAsync(
        PostgresDbContext context,
        bool seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connStr = context.Database.GetConnectionString();
        if (!IsDisposableTarget(connStr))
        {
            throw new InvalidOperationException(
                "EnsureCreated is strictly forbidden on non-disposable environments. Shared schema must evolve via versioned migrations and compatibility checks.");
        }

        var createdFromModel = await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        // EnsureCreated owns the model tables; the migration ledger still records the frozen
        // baseline as adopted and applies the supplement, security and index scripts so dbinit
        // and migrate converge on the same metadata and version.
        await PostgresSchemaMigrations.ApplyMigrationsAsync(context, cancellationToken).ConfigureAwait(false);

        var securitySql = await ReadSecurityScriptAsync(cancellationToken).ConfigureAwait(false);

        CanonicalCatalogSeedSummary? seedSummary = null;
        if (seed)
        {
            seedSummary = await PostgresCanonicalCatalogSeeder
                .IngestEmbeddedManifestAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }

        return new SchemaBootstrapResult(
            createdFromModel,
            CountStatements(securitySql),
            ComputeModelFingerprint(context),
            seedSummary);
    }

    public static Task<string> ReadSecurityScriptAsync(CancellationToken cancellationToken = default) =>
        ReadEmbeddedScriptAsync(SecurityScriptResourceName, cancellationToken);

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
