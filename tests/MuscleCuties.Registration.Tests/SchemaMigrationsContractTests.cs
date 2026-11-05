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
}
