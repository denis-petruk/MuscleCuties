using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

// Versioned catalog rows are read-only to the application.
internal sealed class ReviewedTemplateFloorRow
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public int MinimumWorkingSets { get; set; }
    public int MinimumExercises { get; set; }
    public string SlotFloorsJson { get; set; } = string.Empty;
    public string? Reviewer { get; set; }
    public string? EvidenceReference { get; set; }
    public string? EvidenceSha256 { get; set; }
    public DateOnly? ReviewedOn { get; set; }
}

internal static class ReviewedTemplateFloorSource
{
    internal static Task<List<ReviewedTemplateFloorRow>> LoadAsync(PostgresDbContext context, CancellationToken cancellationToken) =>
        context.Database.SqlQueryRaw<ReviewedTemplateFloorRow>("""
            SELECT template_id AS "TemplateId", template_version AS "TemplateVersion",
                   minimum_working_sets AS "MinimumWorkingSets", minimum_exercises AS "MinimumExercises",
                   slot_floors::text AS "SlotFloorsJson", reviewer AS "Reviewer",
                   evidence_reference AS "EvidenceReference", evidence_sha256 AS "EvidenceSha256", reviewed_on AS "ReviewedOn"
            FROM catalog.workout_template_volume_floors ORDER BY template_id
            """).ToListAsync(cancellationToken);

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
