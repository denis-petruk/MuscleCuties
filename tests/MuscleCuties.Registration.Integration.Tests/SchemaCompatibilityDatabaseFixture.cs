using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[CollectionDefinition(Name)]
public sealed class SchemaDatabaseCollection : ICollectionFixture<SchemaCompatibilityDatabaseFixture>
{
    public const string Name = "schema-database";
}

/// <summary>
/// Builds one disposable PostgreSQL fixture by rehearsing the exact source chain to the historical
/// 31-migration boundary and then to the current head. Every test mutates an isolated copy.
/// The fixture refuses any host other than loopback and any database name outside <c>mc_test_</c>.
/// </summary>
public sealed class SchemaCompatibilityDatabaseFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "MC_SCHEMA_COMPATIBILITY_POSTGRES";
    public const string TemplateDatabase = "mc_test_schema_template";
    public const string BaselineMigrationId = "20261024200000_AddWorkoutExerciseExclusionsV010";
    public const string FinalMigrationId = "20261028190000_AddWorkoutCycleReadinessOccurrences";
    private const string LoopbackHost = "127.0.0.1";
    private const string DatabaseNamePattern = "^mc_test_[a-z0-9_]+$";
    private const int MaximumDatabaseNameLength = 63;
    private static int copyCounter;

    public string BaseConnectionString { get; private set; } = string.Empty;
    public bool Available { get; private set; }
    public string TemplateConnectionString => WithDatabase(TemplateDatabase);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return;

        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (!string.Equals(builder.Host, LoopbackHost, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{EnvironmentVariable} must target {LoopbackHost}; refusing to run destructive fixtures elsewhere.");

        BaseConnectionString = builder.ConnectionString;
        Available = true;
        await RecreateAsync(TemplateDatabase);
        await BootstrapAsync(TemplateConnectionString);
        await MigrateAsync(TemplateConnectionString, BaselineMigrationId);
        await MigrateAsync(TemplateConnectionString, null);

        // The raw 31→59 chain omits two application grants. The fixture baseline is the intended
        // target state, so isolated-copy mutation tests start from a compatible schema. The
        // rehearsal test separately proves the raw chain gap and the additive correction.
        var candidate = Path.Combine(FindRoot(), "src", "MuscleCuties.DbMaintenance", "Schema", "Reconciliation",
            "20261029_grant_workout_calendar_app_access.sql");
        await ExecuteAsync(TemplateConnectionString, await File.ReadAllTextAsync(candidate));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task<string> CreateCopyAsync(string suffix)
    {
        var name = $"mc_test_case_{Interlocked.Increment(ref copyCounter)}_{suffix}";
        await RecreateFromTemplateAsync(name);
        return WithDatabase(name);
    }

    public async Task DropAsync(string connectionString)
    {
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using var connection = new NpgsqlConnection(WithDatabase("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        NpgsqlConnection.ClearAllPools();
    }

    public string WithDatabase(string database)
    {
        if (database != "postgres" &&
            (!Regex.IsMatch(database, DatabaseNamePattern) || database.Length > MaximumDatabaseNameLength))
            throw new InvalidOperationException($"Refusing database name '{database}' outside the disposable fixture namespace.");
        return new NpgsqlConnectionStringBuilder(BaseConnectionString) { Database = database }.ConnectionString;
    }

    public static async Task MigrateAsync(string connectionString, string? targetMigration)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new PostgresDbContext(options);
        if (targetMigration is null)
            await context.Database.MigrateAsync();
        else
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    public static async Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new PostgresDbContext(options);
        return (await context.Database.GetAppliedMigrationsAsync()).Order(StringComparer.Ordinal).ToArray();
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
    }

    private async Task RecreateAsync(string database)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(WithDatabase("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE); CREATE DATABASE \"{database}\" TEMPLATE template0 ENCODING 'UTF8'",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RecreateFromTemplateAsync(string database)
    {
        // PostgreSQL refuses CREATE DATABASE ... TEMPLATE while any backend is attached to the
        // template. Npgsql pooling keeps idle backends alive, so drop the pools first.
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(WithDatabase("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE); CREATE DATABASE \"{database}\" TEMPLATE \"{TemplateDatabase}\"",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task BootstrapAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            DO $$
            DECLARE role_name text;
            BEGIN
                FOREACH role_name IN ARRAY ARRAY[
                    'musclecuties_app', 'musclecuties_auth', 'musclecuties_migrator',
                    'musclecuties_cleanup_dispatch', 'musclecuties_cleanup_discovery_owner',
                    'musclecuties_photo_purge_stage_owner', 'musclecuties_photo_reconcile',
                    'musclecuties_photo_reconcile_owner', 'musclecuties_workout_discovery_owner',
                    'musclecuties_workout_dispatch'
                ] LOOP
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                        EXECUTE format('CREATE ROLE %I NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE', role_name);
                    END IF;
                END LOOP;
            END $$;
            CREATE SCHEMA IF NOT EXISTS app;
            CREATE SCHEMA IF NOT EXISTS catalog;
            CREATE SCHEMA IF NOT EXISTS ops;
            GRANT USAGE, CREATE ON SCHEMA catalog TO musclecuties_migrator;
            GRANT USAGE ON SCHEMA app, catalog, ops TO musclecuties_migrator;
            ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT ALL ON TABLES TO musclecuties_migrator;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
}
