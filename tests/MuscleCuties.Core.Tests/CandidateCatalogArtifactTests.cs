using MuscleCuties.Core.Domain.Workout.Planning.Catalog;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// The candidate document is generated, never hand-edited. One test writes it on request; the other
/// proves the committed artifact still equals what the rules produce today.
/// </summary>
public sealed class CandidateCatalogArtifactTests
{
    private const string RelativePath = "docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json";

    [Fact]
    public void Committed_candidate_document_matches_generated_output()
    {
        var path = FindArtifact();
        Assert.True(File.Exists(path), $"Candidate metadata document is missing at {path}.");

        var committed = File.ReadAllText(path);
        Assert.Equal(CatalogAuditFixture.CandidateDocument(), committed);
    }

    [Theory]
    [InlineData("MC_WRITE_CANDIDATE_METADATA")]
    public void Writes_candidate_document_when_explicitly_requested(string variable)
    {
        var target = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(target))
            return;

        File.WriteAllText(target, CatalogAuditFixture.CandidateDocument());
        Assert.True(File.Exists(target));
    }

    private static string FindArtifact()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, RelativePath);
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository root for the candidate metadata document was not found.");
    }
}
