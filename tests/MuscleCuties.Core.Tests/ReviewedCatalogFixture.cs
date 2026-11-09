using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>Maps the reviewed V0.2 catalog snapshot into planner catalog entries for Core-level guards.</summary>
internal static class ReviewedCatalogFixture
{
    private static readonly Lazy<IReadOnlyList<StrengthExerciseCatalogEntry>> Entries = new(Load);
    private static readonly Lazy<JsonDocument> Document = new(() =>
        JsonDocument.Parse(File.ReadAllText(FindSnapshot())));

    public static IReadOnlyList<StrengthExerciseCatalogEntry> Catalog => Entries.Value;

    public static JsonElement Root => Document.Value.RootElement;

    public static StrengthExerciseCatalogEntry Entry(int id) =>
        Catalog.Single(entry => entry.Candidate.ExerciseId == id);

    public static IReadOnlySet<ExerciseEquipmentKind> Equipment(EquipmentSet required)
    {
        if (required == EquipmentSet.None) return new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight };
        var map = new Dictionary<EquipmentSet, ExerciseEquipmentKind>
        {
            [EquipmentSet.Barbell] = ExerciseEquipmentKind.Barbell,
            [EquipmentSet.Dumbbell] = ExerciseEquipmentKind.Dumbbell,
            [EquipmentSet.Bands] = ExerciseEquipmentKind.ResistanceBand,
            [EquipmentSet.Cables] = ExerciseEquipmentKind.Cable,
            [EquipmentSet.Machines] = ExerciseEquipmentKind.Machine,
            [EquipmentSet.HipThrustBench] = ExerciseEquipmentKind.HipThrustBench,
            [EquipmentSet.BackExtension45] = ExerciseEquipmentKind.BackExtension45,
            [EquipmentSet.PullUpBar] = ExerciseEquipmentKind.PullUpBar,
            [EquipmentSet.Kettlebell] = ExerciseEquipmentKind.Kettlebell,
            [EquipmentSet.ClimbingWall] = ExerciseEquipmentKind.ClimbingWall
        };
        return map.Where(pair => (required & pair.Key) == pair.Key).Select(pair => pair.Value).ToHashSet();
    }

    private static IReadOnlyList<StrengthExerciseCatalogEntry> Load()
    {
        var root = Root;
        var version = root.GetProperty("exerciseReviewVersion").GetString()!;
        return root.GetProperty("exercises").EnumerateArray().Select(exercise =>
        {
            var reviews = exercise.GetProperty("directedSubstitutionReviews").EnumerateArray()
                .Select(pair => new DirectedSubstitutionReview(pair.GetProperty("targetSourceExerciseId").GetInt32(),
                    Enum.Parse<DirectedSubstitutionCategory>(pair.GetProperty("category").GetString()!),
                    pair.GetProperty("conditions").GetString()!))
                .ToArray();
            var candidate = new ReviewedExerciseCandidate(
                exercise.GetProperty("sourceExerciseId").GetInt32(),
                version,
                Enum.Parse<MovementPattern>(exercise.GetProperty("expectedPattern").GetString()!),
                Equipment((EquipmentSet)exercise.GetProperty("expectedRequiredEquipment").GetInt32()),
                Strings(exercise, "supportedExperiences").Select(Enum.Parse<ActivityExperience>).ToHashSet(),
                Strings(exercise, "demandTags").Select(Enum.Parse<ActivityDemandTag>).ToHashSet(),
                true,
                true,
                new HashSet<ActivityDemandTag>(),
                Strings(exercise, "supportedTimingMethods").Select(Enum.Parse<ExerciseTimingMethod>).ToHashSet(),
                exercise.GetProperty("expectedSetupSeconds").GetInt32() / 60m,
                reviews.Select(review => review.TargetExerciseId).ToHashSet(),
                (InjuryFlag)exercise.GetProperty("expectedContraindications").GetInt32(),
                exercise.GetProperty("experienceConditions").GetString(),
                null,
                true,
                reviews);
            return new StrengthExerciseCatalogEntry(exercise.GetProperty("expectedName").GetString()!, candidate, []);
        }).ToArray();
    }

    private static IEnumerable<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(value => value.GetString()!);

    private static string FindSnapshot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MuscleCuties.Infrastructure.PostgreSql",
                "Workout", "Planning", "ReviewData", "ReviewedExerciseCatalogV020.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Reviewed exercise catalog snapshot was not found.");
    }
}
