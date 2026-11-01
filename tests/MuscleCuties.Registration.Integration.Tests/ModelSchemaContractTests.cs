using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Model-first contract guard. There is no EF migration snapshot to compare against, so these tests
/// assert the invariants the schema bootstrap relies on: every mapped table lands in a registered
/// schema, and the fingerprint used for drift detection is deterministic.
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

    [Xunit.Fact]
    public void EveryMappedTableLivesInARegisteredSchema()
    {
        using var context = CreateContext();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
                continue;

            var schema = entityType.GetSchema() ?? entityType.GetDefaultSchema();
            Xunit.Assert.Contains(schema, RegisteredSchemas);
        }
    }

    [Xunit.Fact]
    public void ModelFingerprintIsDeterministic()
    {
        using var first = CreateContext();
        using var second = CreateContext();

        Xunit.Assert.Equal(
            PostgresSchemaBootstrap.ComputeModelFingerprint(first),
            PostgresSchemaBootstrap.ComputeModelFingerprint(second));
    }

    [Xunit.Fact]
    public void SecurityScriptDeclaresTenantPolicies()
    {
        var script = PostgresSchemaBootstrap.ReadSecurityScriptAsync().GetAwaiter().GetResult();

        Xunit.Assert.Contains("ENABLE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        Xunit.Assert.Contains("CREATE POLICY", script, StringComparison.Ordinal);
        Xunit.Assert.Contains("CREATE OR REPLACE FUNCTION", script, StringComparison.Ordinal);
    }

    private static PostgresDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(UnusedLocalConnection)
            .Options);
}
