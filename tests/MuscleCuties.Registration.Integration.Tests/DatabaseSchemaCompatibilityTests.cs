using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Every test mutates an isolated copy of the freshly rehearsed 59-migration database. Assertions
/// are deliberately strict: the evaluator must detect the fault and must never call the schema
/// compatible while it is present.
/// </summary>
[Collection(SchemaDatabaseCollection.Name)]
public sealed class DatabaseSchemaCompatibilityTests(
    SchemaCompatibilityDatabaseFixture fixture,
    ITestOutputHelper output)
{
    [Fact]
    public async Task CompatibleFixtureIsReportedCompatible()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("compatible");
        try
        {
            var report = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(report);
            Assert.Equal(SchemaCompatibilityStatus.Compatible, report.Status);
            Assert.Equal(0, report.ExitCode);
            Assert.Empty(report.FailureCodes);
            Assert.Equal(MigrationHistoryModes.ExactSource, report.MigrationHistoryMode);
            Assert.Equal(59, report.SourceMigrationCount);
            Assert.Equal(59, report.AppliedMigrationCount);
            Assert.Equal(64, report.DatabaseFingerprintSha256.Length);
            Assert.Equal(report.RequirementsSha256, DatabaseSchemaCompatibility.ComputeFileSha256(RequirementsPath()));
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task MissingClearedDateIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("missing_cleared_date",
            "ALTER TABLE app.workout_injury_logs DROP COLUMN cleared_date",
            SchemaFailureCodes.RequiredColumnMissing, "app.workout_injury_logs.cleared_date");
    }

    [Fact]
    public async Task MissingConfirmedActiveSecondsIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("missing_active_seconds",
            "ALTER TABLE app.workout_session_logs DROP COLUMN confirmed_active_seconds",
            SchemaFailureCodes.RequiredColumnMissing, "app.workout_session_logs.confirmed_active_seconds");
    }

    [Fact]
    public async Task WrongColumnTypeIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("wrong_type",
            "ALTER TABLE app.workout_session_logs ALTER COLUMN confirmed_active_seconds TYPE bigint",
            SchemaFailureCodes.TypeMismatch, "app.workout_session_logs.confirmed_active_seconds");
    }

    [Fact]
    public async Task WrongNullabilityAndDefaultAreReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("nullability_default");
        try
        {
            await ExecuteAsync(connectionString, """
                ALTER TABLE app.workout_session_logs ALTER COLUMN confirmed_active_seconds DROP NOT NULL;
                ALTER TABLE app.workout_session_logs ALTER COLUMN confirmed_active_seconds DROP DEFAULT;
                """);
            var report = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(report);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, report.Status);
            Assert.Contains(SchemaFailureCodes.NullabilityMismatch, report.FailureCodes);
            Assert.Contains(SchemaFailureCodes.DefaultMismatch, report.FailureCodes);
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task MissingTableIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("missing_table",
            "DROP TABLE app.workout_injury_episodes CASCADE",
            SchemaFailureCodes.RequiredTableMissing, "app.workout_injury_episodes");
    }

    [Fact]
    public async Task MissingCompositeTenantForeignKeyIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("missing_fk",
            "ALTER TABLE app.workout_cycle_readiness_occurrences DROP CONSTRAINT fk_workout_cycle_readiness_occurrences_day",
            SchemaFailureCodes.RequiredConstraintMissing, "app.workout_cycle_readiness_occurrences");
    }

    [Fact]
    public async Task MissingAndMisshapedIndexesAreReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("index_shape");
        try
        {
            await ExecuteAsync(connectionString,
                "DROP INDEX app.ix_workout_cycle_readiness_occurrences_user_id_workout_day_id");
            var missing = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(missing);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, missing.Status);
            Assert.Contains(SchemaFailureCodes.RequiredIndexMissing, missing.FailureCodes);

            await ExecuteAsync(connectionString,
                "CREATE UNIQUE INDEX ix_workout_cycle_readiness_occurrences_user_id_workout_day_id ON app.workout_cycle_readiness_occurrences(user_id)");
            var misshaped = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(misshaped);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, misshaped.Status);
            Assert.Contains(SchemaFailureCodes.IndexMismatch, misshaped.FailureCodes);
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task DisabledRowLevelSecurityIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("rls_disabled",
            "ALTER TABLE app.workout_injury_logs DISABLE ROW LEVEL SECURITY",
            SchemaFailureCodes.RlsMismatch, "app.workout_injury_logs");
    }

    [Fact]
    public async Task DroppedTenantPolicyIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("policy_dropped",
            "DROP POLICY tenant_isolation ON app.hiit_evidence_capture_challenges",
            SchemaFailureCodes.PolicyMissing, "app.hiit_evidence_capture_challenges.tenant_isolation");
    }

    [Fact]
    public async Task RevokedExecuteGrantIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("grant_revoked",
            "REVOKE EXECUTE ON FUNCTION app.discover_pending_workout_owners(uuid, integer) FROM musclecuties_workout_dispatch",
            SchemaFailureCodes.GrantMismatch,
            "app.discover_pending_workout_owners(p_after uuid, p_limit integer)");
    }

    [Fact]
    public async Task DroppedDiscoveryFunctionIsReported()
    {
        if (!fixture.Available) return;
        await AssertMutationReports("function_dropped",
            "DROP FUNCTION app.lock_workout_planning_catalog()",
            SchemaFailureCodes.FunctionMissing, "app.lock_workout_planning_catalog()");
    }

    [Fact]
    public async Task UndeclaredRoleIsReportedWithoutMutatingTheCluster()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("missing_role");
        try
        {
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            requirements.Roles.Add(new SchemaRoleRequirement { Name = "musclecuties_absent_probe" });
            var report = await EvaluateAsync(connectionString, requirements);
            Log(report);
            Assert.Contains(SchemaFailureCodes.RoleMissing, report.FailureCodes);
            Assert.Contains(report.Issues, issue => issue.Object == "role:musclecuties_absent_probe");
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task ForbiddenRoleMembershipIsReported()
    {
        if (!fixture.Available) return;
        const string probeRole = "mc_test_membership_probe";
        var connectionString = await fixture.CreateCopyAsync("forbidden_membership");
        try
        {
            await ExecuteAsync(connectionString, $"DROP ROLE IF EXISTS {probeRole}; CREATE ROLE {probeRole} NOLOGIN NOSUPERUSER NOBYPASSRLS");
            await ExecuteAsync(connectionString, $"GRANT {probeRole} TO musclecuties_app");
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            requirements.Roles.Add(new SchemaRoleRequirement { Name = probeRole });
            requirements.Roles.Single(role => role.Name == "musclecuties_app")
                .ForbiddenMemberships.Add(probeRole);
            var report = await EvaluateAsync(connectionString, requirements);
            Log(report);
            Assert.Contains(SchemaFailureCodes.RoleMembershipMismatch, report.FailureCodes);
        }
        finally
        {
            await ExecuteAsync(connectionString, $"REVOKE {probeRole} FROM musclecuties_app; DROP ROLE IF EXISTS {probeRole}");
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task PendingMigrationHistoryIsReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("pending_history");
        try
        {
            await ExecuteAsync(connectionString,
                $"DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{SchemaCompatibilityDatabaseFixture.FinalMigrationId}'");
            var report = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(report);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, report.Status);
            Assert.Contains(SchemaFailureCodes.MigrationHistoryPending, report.FailureCodes);
            Assert.Equal(58, report.AppliedMigrationCount);
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task UnknownAppliedMigrationIsReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("unknown_history");
        try
        {
            await ExecuteAsync(connectionString,
                "INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20990101000000_NotARealMigration', '10.0.3')");
            var report = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(report);
            Assert.Equal(MigrationHistoryModes.UnknownApplied, report.MigrationHistoryMode);
            Assert.Contains(SchemaFailureCodes.MigrationHistoryUnsupported, report.FailureCodes);
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task TargetIdentityMismatchIsReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("target_identity");
        try
        {
            var report = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, CanonicalRequirements());
            Log(report);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, report.Status);
            Assert.Contains(SchemaFailureCodes.TargetIdentityMismatch, report.FailureCodes);
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public async Task ModelTableCountDriftIsReported()
    {
        if (!fixture.Available) return;
        var connectionString = await fixture.CreateCopyAsync("model_count");
        try
        {
            var requirements = RequirementsFor(DatabaseOf(connectionString));
            requirements.ModelContract.ExpectedTableCount = int.MaxValue;
            var report = await EvaluateAsync(connectionString, requirements);
            Log(report);
            Assert.Contains(SchemaFailureCodes.ModelContractMismatch, report.FailureCodes);
            output.WriteLine($"observed_model_table_count={string.Join(',', report.FailureCodes)}");
            var issue = report.Issues.Single(item => item.Code == SchemaFailureCodes.ModelContractMismatch);
            output.WriteLine($"model_table_count_actual={issue.Actual}");
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    [Fact]
    public void RequirementsHashMismatchFailsClosed()
    {
        var exception = Assert.Throws<SchemaRequirementsException>(() =>
            DatabaseSchemaCompatibility.LoadRequirements(RequirementsPath(), new string('0', 64)));
        Assert.Equal(SchemaFailureCodes.RequirementsHashMismatch, exception.Code);
    }

    [Fact]
    public void UnsupportedContractVersionFailsClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mc_requirements_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, File.ReadAllText(RequirementsPath())
                .Replace("\"contractVersion\": \"1.0.0\"", "\"contractVersion\": \"9.9.9\"", StringComparison.Ordinal));
            var exception = Assert.Throws<SchemaRequirementsException>(() =>
                DatabaseSchemaCompatibility.LoadRequirements(path, DatabaseSchemaCompatibility.ComputeFileSha256(path)));
            Assert.Equal(SchemaFailureCodes.ContractVersionUnsupported, exception.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private async Task AssertMutationReports(string suffix, string sql, string code, string objectName)
    {
        var connectionString = await fixture.CreateCopyAsync(suffix);
        try
        {
            await ExecuteAsync(connectionString, sql);
            var report = await EvaluateAsync(connectionString, RequirementsFor(DatabaseOf(connectionString)));
            Log(report);
            Assert.Equal(SchemaCompatibilityStatus.Incompatible, report.Status);
            Assert.Contains(code, report.FailureCodes);
            Assert.Contains(report.Issues, issue => issue.Code == code &&
                (issue.Object == objectName || issue.Object.StartsWith(objectName + ":", StringComparison.Ordinal) ||
                 issue.Object.StartsWith(objectName + ".", StringComparison.Ordinal)));
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    private static Task<SchemaCompatibilityResult> EvaluateAsync(string connectionString, SchemaRequirements requirements) =>
        DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements);

    private static Task ExecuteAsync(string connectionString, string sql) =>
        SchemaCompatibilityDatabaseFixture.ExecuteAsync(connectionString, sql);

    private static string DatabaseOf(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Database ??
        throw new InvalidOperationException("The fixture connection string must name a database.");

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

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private void Log(SchemaCompatibilityResult report)
    {
        output.WriteLine($"status={report.Status} mode={report.MigrationHistoryMode} fingerprint={report.DatabaseFingerprintSha256}");
        foreach (var issue in report.Issues)
            output.WriteLine($"issue={issue.Code} object={issue.Object} expected={issue.Expected} actual={issue.Actual}");
    }
}
