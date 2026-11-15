using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Loads both catalog sources from disk: the reviewed snapshot and the canonical seed manifest.
/// Provenance is derived here, by the adapter, exactly as a production reader would.
/// </summary>
internal static class CatalogAuditFixture
{
    private static readonly Lazy<IDictionary<int, DefinitionRow>> DefinitionRows = new(LoadDefinitions);
    private static readonly Lazy<IDictionary<int, IReadOnlyList<CatalogMuscleRow>>> ContributionRows = new(LoadContributions);
    private static readonly Lazy<IDictionary<int, ReviewRow>> ReviewRows = new(LoadReviews);
    private static readonly Lazy<IReadOnlySet<int>> SlotCandidateIds = new(LoadSlotCandidateIds);

    public static IReadOnlyDictionary<int, DefinitionRow> Definitions => (IReadOnlyDictionary<int, DefinitionRow>)DefinitionRows.Value;
    public static IReadOnlyDictionary<int, ReviewRow> Reviews => (IReadOnlyDictionary<int, ReviewRow>)ReviewRows.Value;
    public static IReadOnlyDictionary<int, IReadOnlyList<CatalogMuscleRow>> Contributions => (IReadOnlyDictionary<int, IReadOnlyList<CatalogMuscleRow>>)ContributionRows.Value;
    public static IReadOnlySet<int> SlotCandidates => SlotCandidateIds.Value;

    public static IReadOnlyList<CatalogExerciseRecord> Records => Definitions.Keys
        .OrderBy(id => id)
        .Select(id => Record(id))
        .ToArray();

    public static IReadOnlyList<CatalogExerciseContributionAuditInput> ContributionInputs => Definitions.Keys
        .OrderBy(id => id)
        .Select(id => new CatalogExerciseContributionAuditInput(
            id,
            Definitions[id].Name,
            Reviews.ContainsKey(id),
            (MovementPattern)Definitions[id].Pattern,
            Contributions.GetValueOrDefault(id, [])))
        .ToArray();

    public static CatalogContributionAudit Audit =>
        ExerciseCatalogAuditRule.AnalyzeContributions(ContributionInputs);

    public static CatalogExerciseRecord Record(int exerciseId) => new(
        exerciseId,
        Reviews.ContainsKey(exerciseId),
        VerifiedFields(exerciseId),
        InheritedFields(exerciseId));

    /// <summary>Fields the reviewed snapshot explicitly covers.</summary>
    public static IReadOnlySet<CatalogMetadataField> VerifiedFields(int exerciseId)
    {
        if (!Reviews.ContainsKey(exerciseId))
            return new HashSet<CatalogMetadataField>();

        return new HashSet<CatalogMetadataField>
        {
            CatalogMetadataField.Identity,
            CatalogMetadataField.MovementPattern,
            CatalogMetadataField.Equipment,
            CatalogMetadataField.SetupTime,
            CatalogMetadataField.SecondsPerRep,
            CatalogMetadataField.Contraindications,
            CatalogMetadataField.SupportedExperiences,
            CatalogMetadataField.ExperienceConditions,
            CatalogMetadataField.DemandTags,
            CatalogMetadataField.RestrictionAssessment,
            CatalogMetadataField.TimingMethods,
            CatalogMetadataField.Substitutions
        };
    }

    /// <summary>Fields that exist only in the canonical seed and are not part of the review.</summary>
    public static IReadOnlySet<CatalogMetadataField> InheritedFields(int exerciseId)
    {
        var fields = new HashSet<CatalogMetadataField>();
        if (Definitions.ContainsKey(exerciseId))
            fields.Add(CatalogMetadataField.UnilateralTime);
        if (Contributions.TryGetValue(exerciseId, out var rows) && rows.Count > 0)
            fields.Add(CatalogMetadataField.MuscleContribution);
        return fields;
    }

    public static CatalogActivationInput ActivationInput(int exerciseId)
    {
        var definition = Definitions[exerciseId];
        var review = Reviews.GetValueOrDefault(exerciseId);
        var contribution = Audit.Exercises.SingleOrDefault(exercise => exercise.ExerciseId == exerciseId);
        return new CatalogActivationInput(
            Record(exerciseId),
            ExerciseCatalogMetadataContract.Evaluate(Record(exerciseId)),
            contribution,
            review?.TimingMethods,
            review?.SupportedExperiences,
            (MovementPattern)definition.Pattern,
            review?.SupportsAerobicContext ?? false);
    }

    public static CatalogSourceAgreement SourceAgreement(int exerciseId)
    {
        var definition = Definitions[exerciseId];
        var review = Reviews[exerciseId];
        return ExerciseCatalogAuditRule.CompareSources(
        [
            new CatalogSourceField(ExerciseCatalogAuditRule.NameField, review.Name, definition.Name),
            new CatalogSourceField(ExerciseCatalogAuditRule.PatternField, review.Pattern, ((MovementPattern)definition.Pattern).ToString()),
            new CatalogSourceField(ExerciseCatalogAuditRule.EquipmentField, review.Equipment.ToString(), definition.Equipment.ToString()),
            new CatalogSourceField(ExerciseCatalogAuditRule.SetupSecondsField, review.SetupSeconds.ToString(), definition.SetupSeconds.ToString()),
            new CatalogSourceField(ExerciseCatalogAuditRule.SecondsPerRepField, review.SecondsPerRep.ToString(), definition.SecondsPerRep.ToString()),
            new CatalogSourceField(ExerciseCatalogAuditRule.ContraindicationsField, review.Contraindications.ToString(), definition.Contraindications.ToString())
        ]);
    }

    public static string CandidateDocument() => CandidateCatalogMetadataDocument.Build(Audit.Exercises);

    public sealed record DefinitionRow(
        int Id,
        string Name,
        int Pattern,
        int Equipment,
        int SetupSeconds,
        int SecondsPerRep,
        bool UnilateralDoublesTime,
        int Contraindications);

    public sealed record ReviewRow(
        int Id,
        string Name,
        string Pattern,
        int Equipment,
        int SetupSeconds,
        int SecondsPerRep,
        int Contraindications,
        IReadOnlySet<ActivityExperience> SupportedExperiences,
        IReadOnlySet<ExerciseTimingMethod> TimingMethods,
        bool SupportsAerobicContext);

    private static IDictionary<int, DefinitionRow> LoadDefinitions()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(FindManifest()));
        var rows = manifest.RootElement.GetProperty("entityTypes").EnumerateArray()
            .First(type => type.GetProperty("entityType").GetString() == "WorkoutExerciseDefinition")
            .GetProperty("rows").EnumerateArray();

        var result = new Dictionary<int, DefinitionRow>();
        foreach (var row in rows)
        {
            var id = row.GetProperty("sourceId").GetInt32();
            result[id] = new DefinitionRow(
                id,
                row.GetProperty("sourceName").GetString()!,
                row.GetProperty("pattern").GetInt32(),
                row.GetProperty("required").GetInt32(),
                row.GetProperty("setupSeconds").GetInt32(),
                row.GetProperty("secondsPerRep").GetInt32(),
                row.GetProperty("unilateralDoublesTime").GetBoolean(),
                row.GetProperty("contraindications").GetInt32());
        }
        return result;
    }

    private static IDictionary<int, IReadOnlyList<CatalogMuscleRow>> LoadContributions()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(FindManifest()));
        var types = manifest.RootElement.GetProperty("entityTypes").EnumerateArray().ToArray();

        var groups = types.First(type => type.GetProperty("entityType").GetString() == "WorkoutMuscleGroup")
            .GetProperty("rows").EnumerateArray()
            .ToDictionary(row => row.GetProperty("sourceId").GetInt32(), row => row.GetProperty("name").GetString()!);

        var result = new Dictionary<int, List<CatalogMuscleRow>>();
        foreach (var row in types.First(type => type.GetProperty("entityType").GetString() == "ExerciseMuscleContribution")
                     .GetProperty("rows").EnumerateArray())
        {
            var exerciseId = row.GetProperty("exerciseId").GetInt32();
            var muscleGroupId = row.GetProperty("muscleGroupId").GetInt32();
            if (!result.TryGetValue(exerciseId, out var list))
                result[exerciseId] = list = [];
            list.Add(new CatalogMuscleRow(muscleGroupId, groups[muscleGroupId], row.GetProperty("fraction").GetDecimal()));
        }
        return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CatalogMuscleRow>)pair.Value);
    }

    private static IDictionary<int, ReviewRow> LoadReviews()
    {
        using var snapshot = JsonDocument.Parse(File.ReadAllText(FindSnapshot()));
        var result = new Dictionary<int, ReviewRow>();
        foreach (var entry in snapshot.RootElement.GetProperty("exercises").EnumerateArray())
        {
            var id = entry.GetProperty("sourceExerciseId").GetInt32();
            result[id] = new ReviewRow(
                id,
                entry.GetProperty("expectedName").GetString()!,
                entry.GetProperty("expectedPattern").GetString()!,
                entry.GetProperty("expectedRequiredEquipment").GetInt32(),
                entry.GetProperty("expectedSetupSeconds").GetInt32(),
                entry.GetProperty("expectedSecondsPerRep").GetInt32(),
                entry.GetProperty("expectedContraindications").GetInt32(),
                Strings(entry, "supportedExperiences").Select(Enum.Parse<ActivityExperience>).ToHashSet(),
                Strings(entry, "supportedTimingMethods").Select(Enum.Parse<ExerciseTimingMethod>).ToHashSet(),
                SupportsAerobicContext(entry));
        }
        return result;
    }

    /// <summary>
    /// An aerobic context exists only where the review explicitly allows timed work outside strength
    /// sets and its timing conditions name the context. Absent evidence stays absent.
    /// </summary>
    private static bool SupportsAerobicContext(JsonElement entry)
    {
        var conditions = entry.GetProperty("timingConditions").GetString();
        return !string.IsNullOrWhiteSpace(conditions) &&
               conditions.Contains("aerobic", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<int> LoadSlotCandidateIds()
    {
        using var snapshot = JsonDocument.Parse(File.ReadAllText(FindSnapshot()));
        return snapshot.RootElement.GetProperty("templateSlots").EnumerateArray()
            .SelectMany(slot => slot.GetProperty("candidateSourceExerciseIds").EnumerateArray())
            .Select(value => value.GetInt32())
            .ToHashSet();
    }

    private static IEnumerable<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(value => value.GetString()!);

    private static string FindManifest() => Find(
        Path.Combine("src", "MuscleCuties.Infrastructure.PostgreSql", "Persistence", "Seed", "CanonicalCatalogManifest.json"),
        "Canonical catalog seed manifest was not found.");

    private static string FindSnapshot() => Find(
        Path.Combine("src", "MuscleCuties.Infrastructure.PostgreSql", "Workout", "Planning", "ReviewData",
            "ReviewedExerciseCatalogV020.json"),
        "Reviewed exercise catalog snapshot was not found.");

    private static string Find(string relativePath, string notFoundMessage)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(notFoundMessage);
    }
}
