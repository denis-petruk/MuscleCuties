namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

/// <summary>One edited portion row, identified by its position in the persisted record.</summary>
public readonly record struct IngredientPortionEdit(int RowIndex, int FoodItemId, float Grams);

/// <summary>
/// NM02: persisting a grouped meal edit. The rows are matched by row identity, not by food, so two
/// occurrences of the same food keep their own portions, and every untouched row keeps its original
/// record instead of being rewritten from a display value.
/// </summary>
public static class GroupedMealEditProjection
{
    public static float[] ResolveAmounts(
        IReadOnlyList<float> originalGrams,
        IReadOnlyList<int> originalFoodItemIds,
        IReadOnlyList<IngredientPortionEdit?> edits)
    {
        if (originalGrams.Count != originalFoodItemIds.Count)
            throw new ArgumentException("Every original portion needs its food identifier.", nameof(originalFoodItemIds));

        var resolved = originalGrams.ToArray();
        var bounds = Math.Min(resolved.Length, edits.Count);
        for (var index = 0; index < bounds; index++)
        {
            if (edits[index] is not { } edit)
                continue;

            // A row only amends the record it was opened from; a mismatched identity is ignored so a
            // stale or reordered row cannot overwrite another ingredient.
            if (edit.RowIndex != index || edit.FoodItemId != originalFoodItemIds[index])
                continue;

            if (!float.IsFinite(edit.Grams) || edit.Grams <= 0)
                continue;

            resolved[index] = edit.Grams;
        }

        return resolved;
    }
}
