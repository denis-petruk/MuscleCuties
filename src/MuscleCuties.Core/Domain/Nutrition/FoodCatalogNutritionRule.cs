namespace MuscleCuties.Core.Domain.Nutrition;

public static class FoodCatalogNutritionRule
{
    private const float MinimumValidNutrient = 0f;

    public static bool HasDisplayableNutrition(FoodCatalogCandidate candidate) =>
        !string.IsNullOrWhiteSpace(candidate.Name) &&
        IsValid(candidate.Calories) && IsValid(candidate.Protein) &&
        IsValid(candidate.Carbs) && IsValid(candidate.Fats);

    private static bool IsValid(float? value) =>
        value is null || (float.IsFinite(value.Value) && value.Value >= MinimumValidNutrient);
}
