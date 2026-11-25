namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Minimum input requirements for a manually logged meal ingredient list.</summary>
public static class MealIngredientWeightRule
{
    public const int MinimumIngredientCount = 1;
    public const float MinimumIngredientGrams = 0f;

    /// <summary>True when the meal carries at least the minimum number of ingredients.</summary>
    public static bool HasIngredients(int count) => count >= MinimumIngredientCount;

    /// <summary>True when the ingredient weight is finite and above zero.</summary>
    public static bool IsValidGrams(float grams) =>
        float.IsFinite(grams) && grams > MinimumIngredientGrams;
}
