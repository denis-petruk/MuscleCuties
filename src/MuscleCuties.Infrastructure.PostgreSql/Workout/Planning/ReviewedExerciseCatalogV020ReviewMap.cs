using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal sealed record ReviewedExerciseSlotMapV020(
    IReadOnlyDictionary<string, IReadOnlySet<int>> AllowedIds,
    IReadOnlyDictionary<string, string> Conditions);

internal static class ReviewedExerciseCatalogV020ReviewMap
{
    private const string ReviewResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Workout.Planning.ReviewData.ReviewedExerciseCatalogV020.json";

    public static ReviewedExerciseSlotMapV020 MapSlots(
        IReadOnlyList<WorkoutExerciseDefinition> definitions)
    {
        using var stream = typeof(ReviewedExerciseCatalogV020ReviewMap).Assembly
            .GetManifestResourceStream(ReviewResourceName);
        if (stream is null)
            return Empty();

        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("policyVersion").GetString() != StrengthPlanPreviewRule.Version ||
            root.GetProperty("templateVersion").GetString() != StrengthTemplateCatalog.Version)
            return Empty();

        var bySeedCode = definitions.ToDictionary(row => row.SeedCode, StringComparer.Ordinal);
        var databaseIdBySourceId = new Dictionary<int, int>();
        foreach (var entry in root.GetProperty("exercises").EnumerateArray())
        {
            var seedCode = entry.GetProperty("seedCode").GetString()!;
            if (!bySeedCode.TryGetValue(seedCode, out var row) ||
                row.Name != entry.GetProperty("expectedName").GetString() ||
                row.Pattern.ToString() != entry.GetProperty("expectedPattern").GetString() ||
                (int)row.Required != entry.GetProperty("expectedRequiredEquipment").GetInt32() ||
                row.SetupSeconds != entry.GetProperty("expectedSetupSeconds").GetInt32() ||
                row.SecondsPerRep != entry.GetProperty("expectedSecondsPerRep").GetInt32() ||
                (int)row.Contraindications != entry.GetProperty("expectedContraindications").GetInt32())
                continue;
            databaseIdBySourceId[entry.GetProperty("sourceExerciseId").GetInt32()] = row.Id;
        }

        var allowed = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal);
        var conditions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in root.GetProperty("templateSlots").EnumerateArray())
        {
            var fullId = slot.GetProperty("templateIdAndVersion").GetString() ?? string.Empty;
            var parts = fullId.Split(" / ", StringSplitOptions.None);
            if (parts.Length != 2 || parts[1] != StrengthTemplateCatalog.Version)
                continue;
            var slotId = slot.GetProperty("movementSlot").GetString() ?? string.Empty;
            var template = StrengthTemplateCatalog.Templates.FirstOrDefault(item =>
                item.TemplateId == parts[0]);
            var templateSlot = template?.Slots.FirstOrDefault(item => item.SlotId == slotId);
            if (templateSlot is null ||
                slot.GetProperty("requiredOrOptional").GetString() !=
                (templateSlot.IsRequired ? "Required" : "Optional") ||
                slot.GetProperty("roleByStyle").GetString() !=
                $"General={templateSlot.GeneralRole}; High volume={templateSlot.HighVolumeRole}; High intensity={(templateSlot.IsHeavyMainInHighIntensity ? "Heavy main" : templateSlot.HighIntensityRole.ToString())}")
                continue;

            var candidateSourceIds = slot.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (candidateSourceIds.Any(id => !databaseIdBySourceId.ContainsKey(id)))
                continue;
            var key = $"{parts[0]}|{slotId}";
            allowed[key] = candidateSourceIds.Select(id => databaseIdBySourceId[id]).ToHashSet();
            conditions[key] = slot.GetProperty("selectionConstraints").GetString() ?? string.Empty;
        }

        return new ReviewedExerciseSlotMapV020(allowed, conditions);
    }

    private static ReviewedExerciseSlotMapV020 Empty() => new(
        new Dictionary<string, IReadOnlySet<int>>(),
        new Dictionary<string, string>());
}
