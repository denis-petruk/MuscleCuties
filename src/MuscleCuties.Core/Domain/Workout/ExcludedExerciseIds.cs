using System.Text.Json;

namespace MuscleCuties.Core.Domain.Workout;

/// <summary>
/// The persisted excluded-exercise list is a JSON array of positive ids. A blank payload, a non-array
/// payload, junk entries and malformed JSON all mean "nothing excluded", never "everything excluded".
/// Planning safety and the profile snapshot both read the same field, so they parse it here.
/// </summary>
public static class ExcludedExerciseIds
{
    public static HashSet<int> Parse(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<int>();
            return document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Number &&
                                value.TryGetInt32(out var exerciseId) && exerciseId > 0)
                .Select(value => value.GetInt32())
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<int>();
        }
    }
}
