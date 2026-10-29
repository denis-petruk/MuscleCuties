using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuscleCuties.DbMaintenance.Schema;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Reconciliation must fail closed until an owner approves exact operations, an exact target
/// fingerprint and an explicit history policy. These tests never mutate a live target.
/// </summary>
[Collection(SchemaDatabaseCollection.Name)]
public sealed class SchemaReconciliationTests(
    SchemaCompatibilityDatabaseFixture fixture,
    ITestOutputHelper output)
{
    private const string AdminFallback = "Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres";

    private string AdminConnectionString => fixture.Available ? fixture.WithDatabase("postgres") : AdminFallback;

    [Fact]
    public async Task ReconciliationFailsClosedWithoutConfirmation()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var manifest = CreateManifest(root, new string('0', 64));
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, sha256, CanonicalRequirements(), root, confirmed: false);
            Assert.Equal(SchemaReconciliation.StatusFailed, outcome.Status);
            Assert.Contains(ReconciliationFailureCodes.NotConfirmed, outcome.FailureCodes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnapprovedManifestIsRejectedBeforeAnyDatabaseWork()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var manifest = CreateManifest(root, new string('0', 64));
            manifest.Approved = false;
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, sha256, CanonicalRequirements(), root, confirmed: true);
            Assert.Equal(SchemaReconciliation.StatusFailed, outcome.Status);
            Assert.Contains(ReconciliationFailureCodes.NotApproved, outcome.FailureCodes);
            Assert.Empty(outcome.Steps);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ManifestHashMismatchIsRejected()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var (path, _) = WriteManifest(root, CreateManifest(root, new string('0', 64)));
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, new string('1', 64), CanonicalRequirements(), root, confirmed: true);
            Assert.Contains(ReconciliationFailureCodes.ManifestHashMismatch, outcome.FailureCodes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OperationPathEscapeIsRejected()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var manifest = CreateManifest(root, new string('0', 64));
            manifest.Operations[0].SqlFile = "Schema/Reconciliation/../escape.sql";
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, sha256, CanonicalRequirements(), root, confirmed: true);
            Assert.Contains(ReconciliationFailureCodes.OperationPathRejected, outcome.FailureCodes);
            Assert.Empty(outcome.Steps);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChangedOperationContentIsRejected()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var manifest = CreateManifest(root, new string('0', 64));
            var (path, sha256) = WriteManifest(root, manifest);
            File.WriteAllText(Path.Combine(root, "Schema", "Reconciliation", "noop.sql"), "SELECT 2;\n");
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, sha256, CanonicalRequirements(), root, confirmed: true);
            Assert.Contains(ReconciliationFailureCodes.OperationHashMismatch, outcome.FailureCodes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DependencyCycleIsRejected()
    {
        var root = CreateRepositoryRoot();
        try
        {
            var manifest = CreateManifest(root, new string('0', 64));
            var sql = "SELECT 1;\n";
            Directory.CreateDirectory(Path.Combine(root, "Schema", "Reconciliation"));
            File.WriteAllText(Path.Combine(root, "Schema", "Reconciliation", "second.sql"), sql);
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            manifest.Operations[0].DependsOn = ["second"];
            manifest.Operations.Add(new ReconciliationOperation
            {
                Id = "second",
                SqlFile = "Schema/Reconciliation/second.sql",
                Sha256 = sha,
                Transaction = SchemaReconciliation.Transactional,
                DependsOn = ["noop"]
            });
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                AdminConnectionString, path, sha256, CanonicalRequirements(), root, confirmed: true);
            Assert.Contains(ReconciliationFailureCodes.DependencyInvalid, outcome.FailureCodes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WrongTargetFingerprintFailsClosed()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("reconcile_wrong_fingerprint");
        var root = CreateRepositoryRoot();
        try
        {
            await SchemaCompatibilityDatabaseFixture.ExecuteAsync(connectionString,
                "ALTER TABLE app.workout_injury_logs DROP COLUMN cleared_date");
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            var preflight = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, preflight.Status);

            var manifest = CreateManifest(root, new string('0', 64));
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                connectionString, path, sha256, requirements, root, confirmed: true);
            Log(outcome);
            Assert.Equal(SchemaReconciliation.StatusFailed, outcome.Status);
            Assert.Contains(ReconciliationFailureCodes.TargetMismatch, outcome.FailureCodes);
            Assert.Empty(outcome.Steps);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task IncompleteOperationLeavesPostconditionFailure()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("reconcile_postcondition");
        var root = CreateRepositoryRoot();
        try
        {
            await SchemaCompatibilityDatabaseFixture.ExecuteAsync(connectionString,
                "ALTER TABLE app.workout_injury_logs DROP COLUMN cleared_date");
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            var preflight = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, preflight.Status);

            var manifest = CreateManifest(root, preflight.DatabaseFingerprintSha256);
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                connectionString, path, sha256, requirements, root, confirmed: true);
            Log(outcome);
            Assert.Equal(SchemaReconciliation.StatusPostconditionFailed, outcome.Status);
            Assert.Contains(ReconciliationFailureCodes.PostconditionFailed, outcome.FailureCodes);
            Assert.Single(outcome.Steps);
            Assert.True(outcome.Steps[0].Committed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task AdvisoryLockIsExclusiveBoundedAndReusable()
    {
        if (!fixture.Available) return;
        await using var first = new NpgsqlConnection(AdminConnectionString);
        await using var second = new NpgsqlConnection(AdminConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();

        using (var lease = await SchemaReconciliation.TryAcquireAdvisoryLockAsync(first))
        {
            Assert.NotNull(lease);
            var blocked = await SchemaReconciliation.TryAcquireAdvisoryLockAsync(second);
            Assert.Null(blocked);
        }

        using var reacquired = await SchemaReconciliation.TryAcquireAdvisoryLockAsync(second);
        Assert.NotNull(reacquired);
        Assert.True(await SchemaReconciliation.ReleaseAdvisoryLockAsync(second));
    }

    [Fact]
    public async Task TransactionalOperationRollsBackOnFailure()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("rollback");
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => ExecuteOperationAsync(connectionString,
                "CREATE TABLE app.mc_rollback_probe (id integer); SELECT 1 / 0;", SchemaReconciliation.Transactional));
            Assert.Equal(0, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'app' AND c.relname = 'mc_rollback_probe'"));
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task NontransactionalFailureLeavesDetectablePartialState()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("partial_state");
        var root = CreateRepositoryRoot();
        try
        {
            await SchemaCompatibilityDatabaseFixture.ExecuteAsync(connectionString,
                "ALTER TABLE app.workout_injury_logs DROP COLUMN cleared_date");
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            var preflight = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);
            var manifest = CreateManifest(root, preflight.DatabaseFingerprintSha256);

            const string firstSql = "CREATE TABLE app.mc_partial_probe (id integer);\n";
            const string secondSql = "CREATE TABLE app.mc_second_probe (id integer);\nSELECT 1 / 0;\n";
            var reconciliationRoot = Path.Combine(root, "Schema", "Reconciliation");
            File.WriteAllText(Path.Combine(reconciliationRoot, "first.sql"), firstSql);
            File.WriteAllText(Path.Combine(reconciliationRoot, "second.sql"), secondSql);
            manifest.Operations =
            [
                new ReconciliationOperation
                {
                    Id = "first", SqlFile = "Schema/Reconciliation/first.sql",
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(firstSql))),
                    Transaction = SchemaReconciliation.Transactional, DependsOn = []
                },
                new ReconciliationOperation
                {
                    Id = "second", SqlFile = "Schema/Reconciliation/second.sql",
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secondSql))),
                    Transaction = SchemaReconciliation.NonTransactional, DependsOn = ["first"]
                }
            ];
            var (path, sha256) = WriteManifest(root, manifest);
            var outcome = await SchemaReconciliation.ExecuteAsync(
                connectionString, path, sha256, requirements, root, confirmed: true);
            Log(outcome);
            Assert.Equal(SchemaReconciliation.StatusFailed, outcome.Status);
            Assert.Contains(ReconciliationFailureCodes.PartialState, outcome.FailureCodes);
            Assert.Equal(2, outcome.Steps.Count);
            Assert.True(outcome.Steps[0].Committed);
            Assert.False(outcome.Steps[1].Committed);
            Assert.Equal(1, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'app' AND c.relname = 'mc_partial_probe'"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task RepeatedTransactionalOperationIsSafe()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("repeat");
        try
        {
            const string sql = "CREATE TABLE IF NOT EXISTS app.mc_repeat_probe (id integer);";
            await ExecuteOperationAsync(connectionString, sql, SchemaReconciliation.Transactional);
            await ExecuteOperationAsync(connectionString, sql, SchemaReconciliation.Transactional);
            Assert.Equal(1, await SchemaCompatibilityDatabaseFixture.ScalarAsync(connectionString,
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'app' AND c.relname = 'mc_repeat_probe'"));
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    private static async Task ExecuteOperationAsync(string connectionString, string sql, string transaction)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SchemaReconciliation.RunOperationAsync(connection, new ReconciliationOperation
        {
            Id = "test",
            SqlFile = "Schema/Reconciliation/test.sql",
            Sha256 = new string('0', 64),
            Transaction = transaction
        }, sql);
    }

    private static ReconciliationManifest CreateManifest(string root, string fingerprint)
    {
        const string sql = "SELECT 1;\n";
        var reconciliationRoot = Path.Combine(root, "Schema", "Reconciliation");
        Directory.CreateDirectory(reconciliationRoot);
        File.WriteAllText(Path.Combine(reconciliationRoot, "noop.sql"), sql);
        return new ReconciliationManifest
        {
            ContractVersion = DatabaseSchemaCompatibility.SupportedContractVersion,
            Mode = SchemaReconciliation.ModeSqlReconciled,
            Approved = true,
            ApprovedBy = "synthetic-test",
            ApprovalReference = "synthetic-test-only",
            TargetFingerprintSha256 = fingerprint,
            ExpectedHistoryMode = MigrationHistoryModes.ExactSource,
            Operations =
            [
                new ReconciliationOperation
                {
                    Id = "noop",
                    SqlFile = "Schema/Reconciliation/noop.sql",
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql))),
                    Transaction = SchemaReconciliation.Transactional,
                    DependsOn = []
                }
            ]
        };
    }

    private static (string Path, string Sha256) WriteManifest(string root, ReconciliationManifest manifest)
    {
        var path = Path.Combine(root, "reconciliation-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
        return (path, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
    }

    private static string CreateRepositoryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mc_reconciliation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static SchemaRequirements CanonicalRequirements()
    {
        var path = RequirementsPath();
        return DatabaseSchemaCompatibility.LoadRequirements(path, DatabaseSchemaCompatibility.ComputeFileSha256(path));
    }

    private static SchemaRequirements RequirementsFor(string database)
    {
        var requirements = CanonicalRequirements();
        requirements.Target.AllowedDatabases.Add(database);
        return requirements;
    }

    private static string RequirementsPath() =>
        Path.Combine(FindRoot(), "src", "MuscleCuties.DbMaintenance", "Schema", "SchemaRequirements.json");

    private static string DatabaseOf(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Database ??
        throw new InvalidOperationException("The fixture connection string must name a database.");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private void Log(ReconciliationOutcome outcome)
    {
        output.WriteLine($"status={outcome.Status} post={outcome.PostReconciliationStatus} fingerprint={outcome.PostFingerprintSha256}");
        foreach (var failure in outcome.FailureCodes) output.WriteLine($"failure={failure}");
        foreach (var step in outcome.Steps) output.WriteLine($"step={step.Id} committed={step.Committed} failure={step.FailureCode}");
    }
}
