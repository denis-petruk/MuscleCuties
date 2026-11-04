using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[CollectionDefinition(Name)]
public sealed class DisposableDatabaseCollection : ICollectionFixture<DisposableDatabaseFixture>
{
    public const string Name = "disposable-database";
}

/// <summary>
/// Builds one disposable PostgreSQL template database by running the same model-first bootstrap the
/// deployment uses: schema from the EF model, then <c>security.sql</c>, then canonical seeding.
/// Every test mutates an isolated copy. The fixture refuses any host other than loopback and any
/// database name outside <c>mc_test_</c>.
/// </summary>
public sealed class DisposableDatabaseFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "MC_SCHEMA_COMPATIBILITY_POSTGRES";
    public const string TemplateDatabase = "mc_test_schema_template";
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

    public static async Task<SchemaBootstrapResult> BootstrapAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new PostgresDbContext(options);
        return await PostgresSchemaBootstrap.InitializeAsync(context, seed: true);
    }

    public static async Task<SchemaDriftReport> InspectAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new PostgresDbContext(options);
        return await PostgresSchemaDriftInspector.InspectAsync(context);
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
}
