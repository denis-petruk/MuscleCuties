namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Validation rules for nutrition log requests shared by app and server use cases.</summary>
public static class NutritionLogRule
{
    public static readonly DateOnly MinimumSupportedDate = new(1970, 1, 1);
    public static readonly DateOnly MaximumSupportedDate = new(2100, 1, 1);

    public static bool IsSupportedDate(DateOnly date) =>
        date >= MinimumSupportedDate && date <= MaximumSupportedDate;

    public static bool HasValidIngredients(IReadOnlyList<float>? ingredientGrams) =>
        ingredientGrams is not null &&
        MealIngredientWeightRule.HasIngredients(ingredientGrams.Count) &&
        ingredientGrams.All(MealIngredientWeightRule.IsValidGrams);

    public static bool IsValidMeal(string? mealType, IReadOnlyList<float>? ingredientGrams) =>
        !string.IsNullOrWhiteSpace(mealType) && HasValidIngredients(ingredientGrams);
}
