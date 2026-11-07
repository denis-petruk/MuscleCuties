using System.Text;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class SchemaMigrationsContractTests
{
    [Fact]
    public void MigrationScripts_AreOrderedMonotonicallyStartingAtOne()
    {
        var scripts = PostgresSchemaMigrations.Scripts;
        Assert.NotEmpty(scripts);
        Assert.Equal(1, scripts[0].Version);

        for (var i = 0; i < scripts.Count; i++)
        {
            Assert.Equal(i + 1, scripts[i].Version);
            Assert.False(string.IsNullOrWhiteSpace(scripts[i].ScriptName));
            Assert.False(string.IsNullOrWhiteSpace(scripts[i].ResourceName));
        }
    }

    [Fact]
    public void MigrationScripts_EmbeddedResourcesExistAndHaveContent()
    {
        var assembly = typeof(PostgresSchemaMigrations).Assembly;
        var scripts = PostgresSchemaMigrations.Scripts;

        foreach (var script in scripts)
        {
            using var stream = assembly.GetManifestResourceStream(script.ResourceName);
            Assert.NotNull(stream);
            Assert.True(stream.Length > 0, $"Script {script.ScriptName} resource {script.ResourceName} was empty.");

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = reader.ReadToEnd();
            Assert.False(string.IsNullOrWhiteSpace(content), $"Script {script.ScriptName} content was whitespace.");
        }
    }

    [Fact]
    public void MigrationScripts_MatchesExpectedCleanMigrationChain()
    {
        var scripts = PostgresSchemaMigrations.Scripts;
        Assert.Equal(4, scripts.Count);

        Assert.Equal(1, scripts[0].Version);
        Assert.Equal("001_base_schema.sql", scripts[0].ScriptName);
        Assert.Equal(PostgresSchemaMigrations.BaseSchemaResourceName, scripts[0].ResourceName);

        Assert.Equal(2, scripts[1].Version);
        Assert.Equal("002_schema_fingerprint.sql", scripts[1].ScriptName);
        Assert.Equal(PostgresSchemaMigrations.SchemaFingerprintResourceName, scripts[1].ResourceName);

        Assert.Equal(3, scripts[2].Version);
        Assert.Equal("003_schedule_seam.sql", scripts[2].ScriptName);
        Assert.Equal(PostgresSchemaMigrations.ScheduleSeamResourceName, scripts[2].ResourceName);

        Assert.Equal(4, scripts[3].Version);
        Assert.Equal("004_session_distance.sql", scripts[3].ScriptName);
        Assert.Equal(PostgresSchemaMigrations.SessionDistanceResourceName, scripts[3].ResourceName);
    }

    [Fact]
    public async Task SessionDistanceMigrationIsAdditiveAndNullable()
    {
        var script = PostgresSchemaMigrations.Scripts.Single(candidate => candidate.Version == 4);
        await using var stream = typeof(PostgresSchemaMigrations).Assembly
            .GetManifestResourceStream(script.ResourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sql = await reader.ReadToEndAsync();

        Assert.Contains("ADD COLUMN IF NOT EXISTS distance_km", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RehearseChainAsync_ProducesVerifiedChecksumsAndMonotonicOrder()
    {
        // QA00: Rehearse clean migration chain (001 -> 002 -> 003 -> 004)
        var rehearsal = await PostgresSchemaMigrations.RehearseChainAsync();

        Assert.Equal(4, rehearsal.Count);
        for (var i = 0; i < rehearsal.Count; i++)
        {
            Assert.Equal(i + 1, rehearsal[i].Version);
            Assert.False(string.IsNullOrWhiteSpace(rehearsal[i].ScriptName));
            Assert.NotNull(rehearsal[i].ChecksumSha256);
            Assert.Equal(64, rehearsal[i].ChecksumSha256.Length);
            Assert.True(rehearsal[i].ChecksumSha256.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
        }

        // Re-rehearsing produces identical checksums (deterministic idempotency)
        var secondRehearsal = await PostgresSchemaMigrations.RehearseChainAsync();
        for (var i = 0; i < rehearsal.Count; i++)
        {
            Assert.Equal(rehearsal[i].ChecksumSha256, secondRehearsal[i].ChecksumSha256);
        }
    }
}
