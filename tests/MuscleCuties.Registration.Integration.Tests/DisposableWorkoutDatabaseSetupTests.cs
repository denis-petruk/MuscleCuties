using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>Explicit local-only preparation; never included in the normal integration suite.</summary>
public sealed class DisposableWorkoutDatabaseSetupTests
{
    private const string SetupVariable = "MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE";
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string DatabasePrefix = "mc_test_";

    [Xunit.Fact]
    public async Task MigrateAndSeedExplicitlySelectedDisposableDatabase()
    {
        if (Environment.GetEnvironmentVariable(SetupVariable) != bool.TrueString) return;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
        Xunit.Assert.Equal("127.0.0.1", connection.Host);
        Xunit.Assert.StartsWith(DatabasePrefix, connection.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                CREATE ROLE musclecuties_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                CREATE ROLE musclecuties_app NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                CREATE ROLE musclecuties_auth NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                CREATE ROLE musclecuties_cleanup_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                CREATE ROLE musclecuties_cleanup_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            EXCEPTION WHEN duplicate_object THEN NULL;
            END $$;
            CREATE SCHEMA IF NOT EXISTS catalog;
            GRANT USAGE, CREATE ON SCHEMA catalog TO musclecuties_migrator;
            ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT ALL ON TABLES TO musclecuties_migrator;
            """);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            GRANT USAGE ON SCHEMA app, catalog TO musclecuties_migrator, musclecuties_app, musclecuties_auth;
            GRANT ALL ON ALL TABLES IN SCHEMA catalog TO musclecuties_migrator;
            GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO musclecuties_app;
            """);
        Xunit.Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var root = FindRoot();
        var summary = await PostgresCanonicalCatalogSeeder.IngestFromManifestAsync(db,
            Path.Combine(root, "src", "MuscleCuties.DbMaintenance", "Seed", "CanonicalCatalogManifest.json"));
        Xunit.Assert.True(summary.TotalManifestRows > 0);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
