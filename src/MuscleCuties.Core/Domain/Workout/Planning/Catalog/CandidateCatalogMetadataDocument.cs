using System.Globalization;
using System.Text;

namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

/// <summary>
/// Builds the candidate-metadata document. The output is deliberately canonical: fixed schema and
/// key order, ascending exercise then target, invariant formatting and no generated timestamps, so
/// two runs over the same input are byte-identical.
/// </summary>
public static class CandidateCatalogMetadataDocument
{
    public const string SchemaVersion = "MC-CANDIDATE-CATALOG-1";
    public const string CandidateStatus = "CandidateNotReviewed";

    public static string Build(IEnumerable<CatalogExerciseContributionAudit>? exercises)
    {
        var ordered = (exercises ?? [])
            .Where(exercise => exercise is not null)
            .OrderBy(exercise => exercise.ExerciseId)
            .ToArray();

        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append("  \"schemaVersion\": \"").Append(SchemaVersion).Append("\",\n");
        builder.Append("  \"status\": \"").Append(CandidateStatus).Append("\",\n");
        builder.Append("  \"exercises\": [\n");

        for (var index = 0; index < ordered.Length; index++)
        {
            var exercise = ordered[index];
            var credits = ExerciseCatalogAuditRule.AccountingCredits(exercise)
                .OrderBy(pair => pair.Key)
                .ToArray();

            builder.Append("    {\n");
            builder.Append("      \"exerciseId\": ").Append(exercise.ExerciseId.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("      \"name\": \"").Append(Escape(exercise.Name)).Append("\",\n");
            builder.Append("      \"pattern\": \"").Append(exercise.Pattern.ToString()).Append("\",\n");
            builder.Append("      \"reviewed\": ").Append(exercise.Reviewed ? "true" : "false").Append(",\n");
            builder.Append("      \"uniqueMaximum\": ").Append(exercise.HasUniqueMaximum ? "true" : "false").Append(",\n");
            builder.Append("      \"maximumGroups\": [");
            builder.AppendJoin(", ", exercise.MaximumGroups.Select(group => $"\"{Escape(group)}\""));
            builder.Append("],\n");
            builder.Append("      \"credits\": [");
            if (credits.Length > 0)
            {
                builder.Append('\n');
                for (var creditIndex = 0; creditIndex < credits.Length; creditIndex++)
                {
                    builder.Append("        { \"target\": \"").Append(credits[creditIndex].Key.ToString())
                        .Append("\", \"credit\": ")
                        .Append(credits[creditIndex].Value.ToString("0.###", CultureInfo.InvariantCulture))
                        .Append(" }");
                    builder.Append(creditIndex == credits.Length - 1 ? "\n" : ",\n");
                }
                builder.Append("      ");
            }
            builder.Append("]\n");
            builder.Append("    }");
            builder.Append(index == ordered.Length - 1 ? "\n" : ",\n");
        }

        builder.Append("  ]\n");
        builder.Append("}\n");
        return builder.ToString();
    }

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
