using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
///     Model-first contract guard. There is no EF migration snapshot to compare against, so these tests
///     assert the invariants the schema bootstrap relies on: every mapped table lands in a registered
///     schema, and the fingerprint used for drift detection is deterministic.
/// </summary>
public sealed class ModelSchemaContractTests
{
    private const string UnusedLocalConnection = "Host=127.0.0.1;Database=mc_test_model_metadata;Username=postgres";

    private static readonly string[] RegisteredSchemas =
    [
        PostgresSchemas.App,
        PostgresSchemas.Catalog,
        PostgresSchemas.Operations
    ];

    [PostgresFact]
    public void EveryMappedTableLivesInARegisteredSchema()
    {
        using var context = CreateContext();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
                continue;

            var schema = entityType.GetSchema() ?? entityType.GetDefaultSchema();
            Assert.Contains(schema, RegisteredSchemas);
        }
    }

    [PostgresFact]
    public void ModelFingerprintIsDeterministic()
    {
        using var first = CreateContext();
        using var second = CreateContext();

        Assert.Equal(
            PostgresSchemaBootstrap.ComputeModelFingerprint(first),
            PostgresSchemaBootstrap.ComputeModelFingerprint(second));
    }

    [PostgresFact]
    public void SecurityScriptDeclaresTenantPolicies()
    {
        var script = PostgresSchemaBootstrap.ReadSecurityScriptAsync().GetAwaiter().GetResult();

        Assert.Contains("ENABLE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY", script, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE FUNCTION", script, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task MigrationChain_ContainsBaseSchemaFingerprintAndScheduleSeam()
    {
        var rehearsal = await PostgresSchemaMigrations.RehearseChainAsync();
        Assert.Equal(7, rehearsal.Count);
        Assert.Equal("001_base_schema.sql", rehearsal[0].ScriptName);
        Assert.Equal("002_schema_fingerprint.sql", rehearsal[1].ScriptName);
        Assert.Equal("003_schedule_seam.sql", rehearsal[2].ScriptName);
        Assert.Equal("004_session_distance.sql", rehearsal[3].ScriptName);
        Assert.Equal("005_observed_set_actuals.sql", rehearsal[4].ScriptName);
        Assert.Equal("006_exercise_definition_source_id.sql", rehearsal[5].ScriptName);
        Assert.Equal("007_deload_offer_idempotency.sql", rehearsal[6].ScriptName);
    }

    private static PostgresDbContext CreateContext()
    {
        return new(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(UnusedLocalConnection)
            .Options);
    }
}
