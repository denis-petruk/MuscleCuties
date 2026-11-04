namespace MuscleCuties.Core.Domain.Nutrition;

public static class GeneratedOilPortionRule
{
    public const float CaloriesPerGram = 9f;
    private const float ReferenceOilGrams = 10f;

    public static float CalculatePerIngredientGrams(
        float portionScale, float targetCalories, float targetFats, int oilIngredientCount)
    {
        if (oilIngredientCount <= 0 || !float.IsFinite(portionScale) || portionScale <= 0f ||
            !float.IsFinite(targetCalories) || targetCalories <= 0f ||
            !float.IsFinite(targetFats) || targetFats <= 0f)
            return 0f;

        var totalOilBudget = MathF.Min(PortionCalculationRule.MaxOilGrams,
            MathF.Min(targetFats, targetCalories / CaloriesPerGram));
        return MathF.Min(ReferenceOilGrams * portionScale, totalOilBudget / oilIngredientCount);
    }
}
