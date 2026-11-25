namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Normalises catalogue nutrients to per-100 g, so a comparison never mixes bases.</summary>
public static class FoodCatalogNutritionRule
{
    private const float MinimumValidNutrient = 0f;

    /// <summary>True when the candidate carries enough nutrition to show on a card.</summary>
    public static bool HasDisplayableNutrition(FoodCatalogCandidate candidate) =>
        !string.IsNullOrWhiteSpace(candidate.Name) &&
        IsValid(candidate.Calories) && IsValid(candidate.Protein) &&
        IsValid(candidate.Carbs) && IsValid(candidate.Fats);

    /// <summary>True when the nutrient value is finite and not negative.</summary>
    private static bool IsValid(float? value) =>
        value is null || (float.IsFinite(value.Value) && value.Value >= MinimumValidNutrient);
}
