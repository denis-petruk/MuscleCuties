
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

// Versioned catalog rows are read-only to the application.
/// <summary>One reviewed floor: the minimum volume a template may be reduced to, with the evidence for it.</summary>
internal sealed class ReviewedTemplateFloorRow
{
    /// <summary>The template the floor applies to.</summary>
    public string TemplateId { get; set; } = string.Empty;
    /// <summary>The template version the review covers.</summary>
    public string TemplateVersion { get; set; } = string.Empty;
    /// <summary>Working sets no review may go below for this template.</summary>
    public int MinimumWorkingSets { get; set; }
    /// <summary>Exercises no review may drop below.</summary>
    public int MinimumExercises { get; set; }
    /// <summary>Per-slot minimums as JSON, so a floor can be tightened without a schema change.</summary>
    public string SlotFloorsJson { get; set; } = string.Empty;
    /// <summary>Who reviewed it; null means unreviewed and the floor is not applied.</summary>
    public string? Reviewer { get; set; }
    /// <summary>Where the review evidence lives.</summary>
    public string? EvidenceReference { get; set; }
    /// <summary>Hash of that evidence, so a changed artefact is detectable.</summary>
    public string? EvidenceSha256 { get; set; }
    /// <summary>The date of the review.</summary>
    public DateOnly? ReviewedOn { get; set; }
}

/// <summary>Loads the reviewed floors and matches them to templates, so only a reviewed template is floored.</summary>
internal static class ReviewedTemplateFloorSource
{
    /// <summary>The reviewed volume floors the database holds.</summary>
    internal static Task<List<ReviewedTemplateFloorRow>> LoadAsync(PostgresDbContext context, CancellationToken cancellationToken) =>
        context.Database.SqlQueryRaw<ReviewedTemplateFloorRow>("""
            SELECT template_id AS "TemplateId", template_version AS "TemplateVersion",
                   minimum_working_sets AS "MinimumWorkingSets", minimum_exercises AS "MinimumExercises",
                   slot_floors::text AS "SlotFloorsJson", reviewer AS "Reviewer",
                   evidence_reference AS "EvidenceReference", evidence_sha256 AS "EvidenceSha256", reviewed_on AS "ReviewedOn"
            FROM catalog.workout_template_volume_floors ORDER BY template_id
            """).ToListAsync(cancellationToken);

    /// <summary>The floors keyed by template and version, so a lookup cannot apply another version's floor.</summary>
    internal static IReadOnlyDictionary<string, ReviewedTemplateFloorDto> Map(IEnumerable<ReviewedTemplateFloorRow> rows)
    {
        var result = new Dictionary<string, ReviewedTemplateFloorDto>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var template = StrengthTemplateCatalog.Templates.SingleOrDefault(item => item.TemplateId == row.TemplateId && item.Version == row.TemplateVersion);
            if (template is null) continue;
            Dictionary<string, int>? slots;
            try { slots = JsonSerializer.Deserialize<Dictionary<string, int>>(row.SlotFloorsJson); }
            catch (JsonException) { continue; }
            if (slots is null || !slots.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(template.Slots.Select(slot => slot.SlotId))) continue;
            var floor = new TemplateVolumeFloor(row.TemplateId, row.MinimumWorkingSets, row.MinimumExercises, slots,
                TemplateVolumeFloorRule.ReviewedStatus);
            var candidate = new Dictionary<string, TemplateVolumeFloor> { [row.TemplateId] = floor };
            if (TemplateVolumeFloorRule.GetReviewedFloor(row.TemplateId, candidate) is not null)
                result[row.TemplateId] = new ReviewedTemplateFloorDto(row.TemplateId, row.MinimumWorkingSets,
                    row.MinimumExercises, slots);
        }
        return result;
    }
}
