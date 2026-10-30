using MuscleCuties.DbMaintenance;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[Collection(SchemaDatabaseCollection.Name)]
public sealed class MigrationPrerequisiteTests(SchemaCompatibilityDatabaseFixture fixture)
{
    [Fact]
    public void ReadOnlyPrerequisiteCommandRequiresNoConfirmationOrManifest()
    {
        Assert.True(DatabaseMaintenance.TryParse(["migration-prerequisites"],
            out var command, out var manifestPath, out var expectedSha256));
        Assert.Equal("migration-prerequisites", command);
        Assert.Null(manifestPath);
        Assert.Null(expectedSha256);
    }

    [Fact]
    public async Task PendingOwnershipMigrationRequiresRunnerMembershipBeforeMigrationsRun()
    {
        if (!fixture.Available) return;

        var roleName = $"mc_test_migration_runner_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(fixture.TemplateConnectionString);
        await connection.OpenAsync();
        try
        {
            await ExecuteAsync(connection, $"CREATE ROLE {roleName} NOLOGIN");
            await ExecuteAsync(connection, $"SET ROLE {roleName}");
            var denied = await MigrationPrerequisites.CheckAsync(connection, ownershipMigrationPending: true);
            Assert.False(denied.Ready);
            Assert.Equal("runner_lacks_migrator_membership", denied.Code);

            await ExecuteAsync(connection, "RESET ROLE");
            await ExecuteAsync(connection, $"GRANT musclecuties_migrator TO {roleName}");
            await ExecuteAsync(connection, $"SET ROLE {roleName}");
            var allowed = await MigrationPrerequisites.CheckAsync(connection, ownershipMigrationPending: true);
            Assert.True(allowed.Ready);
            Assert.Equal("ready", allowed.Code);
        }
        finally
        {
            await ExecuteAsync(connection, "RESET ROLE");
            await ExecuteAsync(connection, $"DROP ROLE IF EXISTS {roleName}");
        }
    }

    [Fact]
    public async Task AppliedOwnershipMigrationNeedsNoRunnerMembership()
    {
        if (!fixture.Available) return;
        await using var connection = new NpgsqlConnection(fixture.TemplateConnectionString);
        await connection.OpenAsync();
        var result = await MigrationPrerequisites.CheckAsync(connection, ownershipMigrationPending: false);
        Assert.True(result.Ready);
        Assert.Equal("not_pending", result.Code);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
