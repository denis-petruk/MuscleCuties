namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Minimum input requirements for a manually logged meal ingredient list.</summary>
public static class MealIngredientWeightRule
{
    public const int MinimumIngredientCount = 1;
    public const float MinimumIngredientGrams = 0f;

    public static bool HasIngredients(int count) => count >= MinimumIngredientCount;

    public static bool IsValidGrams(float grams) =>
        float.IsFinite(grams) && grams > MinimumIngredientGrams;
}
