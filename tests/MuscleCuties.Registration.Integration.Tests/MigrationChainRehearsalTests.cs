using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Rehearses the exact observed 31-migration state and the complete pending chain to the current
/// head on a disposable PostgreSQL 17 instance. Live parity remains unverified; this proves the
/// committed EF chain is viable on the observed starting shape.
/// </summary>
[Collection(SchemaDatabaseCollection.Name)]
public sealed class MigrationChainRehearsalTests(
    SchemaCompatibilityDatabaseFixture fixture,
    ITestOutputHelper output)
{
    private const string RehearsalDatabase = "mc_test_rehearsal_31_59";

    [Fact]
    public async Task Historical31ToHead59ChainAppliesCleanlyAndIsRepeatable()
    {
        if (!fixture.Available) return;

        var connectionString = fixture.WithDatabase(RehearsalDatabase);
        await using (var admin = new NpgsqlConnection(fixture.WithDatabase("postgres")))
        {
            await admin.OpenAsync();
            await using var recreate = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{RehearsalDatabase}\" WITH (FORCE); CREATE DATABASE \"{RehearsalDatabase}\" TEMPLATE template0 ENCODING 'UTF8'",
                admin);
            await recreate.ExecuteNonQueryAsync();
        }

        await using (var bootstrap = new NpgsqlConnection(connectionString))
        {
            await bootstrap.OpenAsync();
            await using var roles = new NpgsqlCommand("""
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
                """, bootstrap);
            await roles.ExecuteNonQueryAsync();
        }

        await SchemaCompatibilityDatabaseFixture.MigrateAsync(
            connectionString, SchemaCompatibilityDatabaseFixture.BaselineMigrationId);
        var baseline = await SchemaCompatibilityDatabaseFixture.AppliedMigrationsAsync(connectionString);
        output.WriteLine($"baseline_applied={baseline.Count} baseline_last={baseline[^1]}");
        Assert.Equal(31, baseline.Count);
        Assert.Equal(SchemaCompatibilityDatabaseFixture.BaselineMigrationId, baseline[^1]);
        Assert.Equal(0, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'workout_injury_logs' AND column_name = 'cleared_date'"));
        Assert.Equal(0, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'workout_session_logs' AND column_name = 'confirmed_active_seconds'"));

        await SchemaCompatibilityDatabaseFixture.MigrateAsync(connectionString, null);
        var head = await SchemaCompatibilityDatabaseFixture.AppliedMigrationsAsync(connectionString);
        output.WriteLine($"head_applied={head.Count} head_last={head[^1]}");
        Assert.Equal(59, head.Count);
        Assert.Equal(SchemaCompatibilityDatabaseFixture.FinalMigrationId, head[^1]);

        var requirements = LoadCanonicalRequirements(new NpgsqlConnectionStringBuilder(connectionString).Database ??
            throw new InvalidOperationException("The rehearsal connection string must name a database."));
        var report = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);
        output.WriteLine($"status={report.Status} fingerprint={report.DatabaseFingerprintSha256} mode={report.MigrationHistoryMode}");
        foreach (var issue in report.Issues) output.WriteLine($"issue={issue.Code} object={issue.Object} expected={issue.Expected} actual={issue.Actual}");

        // The committed chain applies every object, policy, role and function, but it never grants
        // musclecuties_app access to two tables it creates. The application cannot use them until a
        // reviewed additive grant operation is approved, so Mode A alone is not release-ready.
        Assert.Equal(SchemaCompatibilityStatus.Incompatible, report.Status);
        Assert.Single(report.FailureCodes);
        Assert.Equal(SchemaFailureCodes.GrantMismatch, report.FailureCodes[0]);
        Assert.Contains(report.Issues, issue =>
            issue.Object == "table:app.workout_account_planning_states" && issue.Expected == "musclecuties_app:SELECT");
        Assert.Contains(report.Issues, issue =>
            issue.Object == "table:app.workout_completion_day_credits" && issue.Expected == "musclecuties_app:SELECT");

        var candidatePath = Path.Combine(FindRoot(), "src", "MuscleCuties.DbMaintenance", "Schema", "Reconciliation",
            "20261029_grant_workout_calendar_app_access.sql");
        Assert.True(File.Exists(candidatePath), "The reviewed additive grant candidate must be committed for rehearsal.");
        await SchemaCompatibilityDatabaseFixture.ExecuteAsync(connectionString, await File.ReadAllTextAsync(candidatePath));
        var reconciled = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);
        output.WriteLine($"reconciled_status={reconciled.Status} fingerprint={reconciled.DatabaseFingerprintSha256}");
        foreach (var issue in reconciled.Issues)
            output.WriteLine($"reconciled_issue={issue.Code} object={issue.Object} expected={issue.Expected} actual={issue.Actual}");
        Assert.Equal(SchemaCompatibilityStatus.Compatible, reconciled.Status);

        Assert.Equal(1, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'workout_injury_logs' AND column_name = 'cleared_date' AND data_type = 'date' AND is_nullable = 'YES'"));
        Assert.Equal(1, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'workout_session_logs' AND column_name = 'confirmed_active_seconds' AND data_type = 'integer' AND is_nullable = 'NO'"));
        Assert.Equal(2, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM pg_roles WHERE rolname IN ('musclecuties_workout_discovery_owner', 'musclecuties_workout_dispatch')"));
        Assert.Equal(2, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'app' AND p.proname IN ('lock_workout_planning_catalog', 'discover_pending_workout_owners')"));
        Assert.Equal(0, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM app.workout_completion_day_credits"));
        Assert.Equal(0, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM app.workout_injury_logs WHERE cleared_date IS NOT NULL"));
        Assert.Equal(1, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
            "SELECT count(*) FROM app.workout_reconciliation_cursor WHERE singleton AND after_owner_id IS NULL"));

        await SchemaCompatibilityDatabaseFixture.MigrateAsync(connectionString, null);
        Assert.Equal(59, (await SchemaCompatibilityDatabaseFixture.AppliedMigrationsAsync(connectionString)).Count);
        Assert.Equal(SchemaCompatibilityStatus.Compatible,
            (await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements)).Status);
    }

    private static SchemaRequirements LoadCanonicalRequirements(string database)
    {
        var path = Path.Combine(FindRoot(), "src", "MuscleCuties.DbMaintenance", "Schema", "SchemaRequirements.json");
        var requirements = DatabaseSchemaCompatibility.LoadRequirements(
            path, DatabaseSchemaCompatibility.ComputeFileSha256(path));
        requirements.Target.AllowedDatabases.Add(database);
        return requirements;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
