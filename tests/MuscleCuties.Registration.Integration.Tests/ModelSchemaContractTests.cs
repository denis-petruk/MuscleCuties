using System.Text;
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
        var chain = PostgresSchemaMigrations.Scripts;
        Assert.Equal(12, chain.Count);
        Assert.Equal("001_base_schema.sql", chain[0].ScriptName);
        Assert.Equal("002_schema_fingerprint.sql", chain[1].ScriptName);
        Assert.Equal("003_schedule_seam.sql", chain[2].ScriptName);
        Assert.Equal("004_session_distance.sql", chain[3].ScriptName);
        Assert.Equal("005_observed_set_actuals.sql", chain[4].ScriptName);
        Assert.Equal("006_exercise_definition_source_id.sql", chain[5].ScriptName);
        Assert.Equal("007_deload_offer_idempotency.sql", chain[6].ScriptName);
        Assert.Equal("008_glute_focus.sql", chain[7].ScriptName);
        Assert.Equal("009_checkin_fatigue.sql", chain[8].ScriptName);
        Assert.Equal("010_unilateral_observed_sets.sql", chain[9].ScriptName);
        Assert.Equal("011_pain_reassessment_timestamps.sql", chain[10].ScriptName);
        Assert.Equal("012_drop_food_sync_logs.sql", chain[11].ScriptName);

        // Every script is reachable, strictly ordered and non-empty: the chain a deployed environment walks.
        var assembly = typeof(PostgresSchemaMigrations).Assembly;
        for (var index = 0; index < chain.Count; index++)
        {
            Assert.Equal(index + 1, chain[index].Version);
            await using var stream = assembly.GetManifestResourceStream(chain[index].ResourceName);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream!, Encoding.UTF8);
            Assert.False(string.IsNullOrWhiteSpace(await reader.ReadToEndAsync()));
        }
    }

    private static PostgresDbContext CreateContext()
    {
        return new(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(UnusedLocalConnection)
            .Options);
    }
}
