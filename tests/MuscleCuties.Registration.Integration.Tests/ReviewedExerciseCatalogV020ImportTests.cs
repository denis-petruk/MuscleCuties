using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class ReviewedExerciseCatalogV020ImportTests
{
    [Fact]
    public void Supplied_Review_Matches_Repository_Snapshot_Without_Claiming_Database_Match()
    {
        using var review = ReadJson("src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewData/ReviewedExerciseCatalogV020.json");
        var definitions = RepositoryDefinitions();

        var result = PostgresReviewedExerciseCatalogImporterV020.InspectSnapshot(
            review.RootElement, definitions);

        Assert.True(result.CanImport, string.Join("; ", result.Issues));
        Assert.Equal(59, result.ExercisesInReview);
        Assert.Equal(59, result.MatchedDatabaseRecords);
        Assert.Equal(160, result.DirectedPairsInReview);
        Assert.Equal(52, result.TemplateSlotsInReview);
        Assert.Equal(59, result.ExerciseRestrictionAssessmentsCompleted);
        Assert.Equal(59, result.OwnerDirectInstructionVerifiedAssessments);
        Assert.Equal(0, result.AutomaticSubstitutionsEnabled);
        Assert.All(review.RootElement.GetProperty("exercises").EnumerateArray(), exercise =>
        {
            Assert.Equal("Assessed", exercise.GetProperty("restrictionAssessmentStatus").GetString());
            Assert.Contains("Owner direct instruction",
                exercise.GetProperty("restrictionAssessmentSource").GetString());
        });
        Assert.Contains("No additional human review is pending",
            review.RootElement.GetProperty("ownerDirectVerification").GetProperty("scope").GetString());
    }

    [Fact]
    public void Changed_Or_Missing_Catalog_Row_Blocks_Import()
    {
        using var review = ReadJson("src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewData/ReviewedExerciseCatalogV020.json");
        var definitions = RepositoryDefinitions().ToList();
        var sourceId = review.RootElement.GetProperty("exercises")[0]
            .GetProperty("sourceExerciseId").GetInt32();
        definitions[0].Name = "Changed exercise";

        var changed = PostgresReviewedExerciseCatalogImporterV020.InspectSnapshot(
            review.RootElement, definitions);
        definitions.RemoveAt(0);
        var missing = PostgresReviewedExerciseCatalogImporterV020.InspectSnapshot(
            review.RootElement, definitions);

        Assert.Contains($"DatabaseExerciseChanged:{sourceId}", changed.Issues);
        Assert.Contains($"DatabaseExerciseMissing:{sourceId}", missing.Issues);
        Assert.False(changed.CanImport);
        Assert.False(missing.CanImport);
    }

    [Fact]
    public void Directed_Reviews_Do_Not_Approve_All_Pairs_As_Automatic_Equivalents()
    {
        using var review = ReadJson("src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewData/ReviewedExerciseCatalogV020.json");
        var pairs = review.RootElement.GetProperty("exercises").EnumerateArray()
            .SelectMany(exercise => exercise.GetProperty("directedSubstitutionReviews").EnumerateArray())
            .ToArray();

        Assert.Equal(70, pairs.Count(pair => pair.GetProperty("category").GetString() ==
            "CloseProgrammingAlternative"));
        Assert.Equal(79, pairs.Count(pair => pair.GetProperty("category").GetString() ==
            "ManualProgrammingAlternative"));
        Assert.Equal(11, pairs.Count(pair => pair.GetProperty("category").GetString() ==
            "RejectedAutomaticSubstitution"));
        Assert.False(review.RootElement.GetProperty("automaticSubstitutionsEnabled").GetBoolean());
        Assert.All(pairs, pair => Assert.False(string.IsNullOrWhiteSpace(
            pair.GetProperty("conditions").GetString())));
    }

    private static IReadOnlyList<WorkoutExerciseDefinition> RepositoryDefinitions()
    {
        using var canonical = ReadJson("src/MuscleCuties.DbMaintenance/Seed/CanonicalCatalogManifest.json");
        var rows = canonical.RootElement.GetProperty("entityTypes").EnumerateArray()
            .Single(section => section.GetProperty("entityType").GetString() ==
                "WorkoutExerciseDefinition")
            .GetProperty("rows").EnumerateArray();
        return rows.Select(row => new WorkoutExerciseDefinition
        {
            SeedCode = row.GetProperty("seedCode").GetString()!,
            Name = row.GetProperty("sourceName").GetString()!,
            Pattern = (MovementPattern)row.GetProperty("pattern").GetInt32(),
            Required = (EquipmentSet)row.GetProperty("required").GetInt32(),
            SetupSeconds = row.GetProperty("setupSeconds").GetInt32(),
            SecondsPerRep = row.GetProperty("secondsPerRep").GetByte(),
            Contraindications = (InjuryFlag)row.GetProperty("contraindications").GetInt32()
        }).ToArray();
    }

    private static JsonDocument ReadJson(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("Repository root was not found.");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName,
            relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
