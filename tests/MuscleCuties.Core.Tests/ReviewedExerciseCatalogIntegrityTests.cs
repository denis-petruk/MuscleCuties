using System.Text.Json;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// P1 catalog validation. The planner may only select and substitute reviewed exercises, so the shipped
/// review snapshot has to stay internally consistent: every entry approved, every entry carrying the
/// metadata generation reads, every substitution target present with a decision that matches its category,
/// and every template candidate inside the reviewed set. These guards read the shipped JSON, so a catalog
/// edit that breaks one of those rules fails the suite instead of reaching a user.
/// </summary>
public sealed class ReviewedExerciseCatalogIntegrityTests
{
    private const string ApprovedWithConditions = "Approved with conditions";
    private const string Rejected = "Rejected";
    private const string RejectedAutomaticSubstitution = "RejectedAutomaticSubstitution";

    /// <summary>
    /// The reviewed snapshot covers every strength planning definition except one cardio activity. The
    /// unreviewed entry is intentional and named here so the gap cannot grow silently, and so reviewing
    /// it later forces this guard and the plan to be updated deliberately.
    /// </summary>
    private const int ExpectedReviewedExercises = 67;
    private const int ExpectedPlanningDefinitions = 68;
    private const string OnlyUnreviewedPlanningDefinitionCatalogCode = "ENGINE_191";
    private const string OnlyUnreviewedPlanningDefinitionName = "Rock Climbing";

    [Fact]
    public void Every_reviewed_exercise_is_approved_and_has_a_unique_source_id()
    {
        using var document = ReviewedCatalog();
        var exercises = document.RootElement.GetProperty("exercises").EnumerateArray().ToArray();

        Assert.Equal(ExpectedReviewedExercises, exercises.Length);
        Assert.Equal(ExpectedReviewedExercises,
            exercises.Select(exercise => exercise.GetProperty("sourceExerciseId").GetInt32()).Distinct().Count());
        Assert.All(exercises, exercise =>
            Assert.Equal(ApprovedWithConditions, exercise.GetProperty("reviewDecision").GetString()));
    }

    [Fact]
    public void Every_reviewed_exercise_carries_the_metadata_generation_reads()
    {
        using var document = ReviewedCatalog();

        foreach (var exercise in document.RootElement.GetProperty("exercises").EnumerateArray())
        {
            var id = exercise.GetProperty("sourceExerciseId").GetInt32();
            Assert.False(string.IsNullOrWhiteSpace(exercise.GetProperty("expectedName").GetString()), $"name {id}");
            Assert.False(string.IsNullOrWhiteSpace(exercise.GetProperty("expectedPattern").GetString()), $"pattern {id}");
            Assert.NotEmpty(exercise.GetProperty("supportedExperiences").EnumerateArray());
            Assert.True(exercise.GetProperty("expectedSetupSeconds").GetInt32() > 0, $"setup {id}");
            // Timed holds legitimately carry no seconds-per-rep; a negative value would be a data error.
            Assert.True(exercise.GetProperty("expectedSecondsPerRep").GetInt32() >= 0, $"reps {id}");
            Assert.True(exercise.GetProperty("expectedRequiredEquipment").GetInt32() >= 0, $"equipment {id}");
            Assert.True(exercise.GetProperty("expectedContraindications").GetInt32() >= 0, $"contraindications {id}");
        }
    }

    [Fact]
    public void Every_substitution_target_is_reviewed_and_its_decision_matches_its_category()
    {
        using var document = ReviewedCatalog();
        var exercises = document.RootElement.GetProperty("exercises").EnumerateArray().ToArray();
        var reviewedIds = exercises.Select(exercise => exercise.GetProperty("sourceExerciseId").GetInt32()).ToHashSet();

        foreach (var exercise in exercises)
        {
            var sourceId = exercise.GetProperty("sourceExerciseId").GetInt32();
            var reviews = exercise.GetProperty("directedSubstitutionReviews").EnumerateArray().ToArray();
            Assert.NotEmpty(reviews);

            foreach (var review in reviews)
            {
                var target = review.GetProperty("targetSourceExerciseId").GetInt32();
                var category = review.GetProperty("category").GetString();
                var decision = review.GetProperty("reviewDecision").GetString();

                Assert.Contains(target, reviewedIds);
                Assert.False(string.IsNullOrWhiteSpace(category), $"category {sourceId}->{target}");

                // A rejected automatic substitution must never be offered as an eligible replacement,
                // and an eligible replacement must be approved with conditions. The JSON has to say so
                // in both fields rather than relying on the caller to check only one.
                if (string.Equals(category, RejectedAutomaticSubstitution, StringComparison.Ordinal))
                    Assert.Equal(Rejected, decision);
                else
                    Assert.Equal(ApprovedWithConditions, decision);
            }
        }
    }

    [Fact]
    public void Every_template_slot_candidate_is_a_reviewed_exercise()
    {
        using var document = ReviewedCatalog();
        var root = document.RootElement;
        var reviewedIds = root.GetProperty("exercises").EnumerateArray()
            .Select(exercise => exercise.GetProperty("sourceExerciseId").GetInt32()).ToHashSet();
        var slots = root.GetProperty("templateSlots").EnumerateArray().ToArray();

        Assert.NotEmpty(slots);
        foreach (var slot in slots)
        {
            var candidates = slot.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            var label = $"{slot.GetProperty("templateIdAndVersion").GetString()} {slot.GetProperty("movementSlot").GetString()}";

            Assert.NotEmpty(candidates);
            Assert.All(candidates, candidate => Assert.True(reviewedIds.Contains(candidate),
                $"{label} offers unreviewed exercise {candidate}"));
        }
    }

    [Fact]
    public void The_only_planning_definition_without_a_review_is_the_known_cardio_activity()
    {
        using var catalog = ReviewedCatalog();
        using var manifest = CanonicalManifest();
        var reviewedSeedCodes = catalog.RootElement.GetProperty("exercises").EnumerateArray()
            .Select(exercise => exercise.GetProperty("seedCode").GetString()!)
            .ToHashSet();

        var planningDefinitions = manifest.RootElement.GetProperty("entityTypes").EnumerateArray()
            .Single(entityType => entityType.GetProperty("entityType").GetString() == "Exercise")
            .GetProperty("rows").EnumerateArray()
            .Where(row => row.TryGetProperty("planningDefinitionSeedCode", out var code) &&
                          !string.IsNullOrWhiteSpace(code.GetString()))
            .Select(row => row.GetProperty("planningDefinitionSeedCode").GetString()!)
            .ToHashSet();

        Assert.Equal(ExpectedPlanningDefinitions, planningDefinitions.Count);
        Assert.All(reviewedSeedCodes, code => Assert.Contains(code, planningDefinitions));

        var unreviewed = planningDefinitions.Except(reviewedSeedCodes).ToArray();
        var onlyUnreviewed = Assert.Single(unreviewed);
        var row = manifest.RootElement.GetProperty("entityTypes").EnumerateArray()
            .Single(entityType => entityType.GetProperty("entityType").GetString() == "Exercise")
            .GetProperty("rows").EnumerateArray()
            .Single(item => item.TryGetProperty("planningDefinitionSeedCode", out var code) &&
                            code.GetString() == onlyUnreviewed);
        Assert.Equal(OnlyUnreviewedPlanningDefinitionCatalogCode, row.GetProperty("catalogCode").GetString());
        Assert.Equal(OnlyUnreviewedPlanningDefinitionName, row.GetProperty("sourceName").GetString());
    }

    private static JsonDocument ReviewedCatalog() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src",
            "MuscleCuties.Infrastructure.PostgreSql", "Workout", "Planning", "ReviewData",
            "ReviewedExerciseCatalogV020.json")));

    private static JsonDocument CanonicalManifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src",
            "MuscleCuties.Infrastructure.PostgreSql", "Persistence", "Seed",
            "CanonicalCatalogManifest.json")));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "MuscleCuties.Infrastructure.PostgreSql")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
