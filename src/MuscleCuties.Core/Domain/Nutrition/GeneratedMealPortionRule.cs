using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>One ingredient's nutrients per 100 g, which portioning scales.</summary>
public sealed record GeneratedMealIngredientProfile(
    string Name,
    float ReferenceGrams,
    float CaloriesPer100Grams,
    float ProteinPer100Grams,
    float CarbsPer100Grams,
    float FatsPer100Grams,
    bool IsOil = false);

/// <summary>One ingredient with the grams chosen for it.</summary>
public sealed record GeneratedMealIngredientPortion(
    GeneratedMealIngredientProfile Profile,
    float Grams,
    MacroNutrients Nutrients);

/// <summary>Sizes a generated meal's ingredients to the calorie target, keeping whole portions where it can.</summary>
public static class GeneratedMealPortionRule
{
    private const float NutrientReferenceGrams = 100f;
    private const float MaximumOilCalorieShare = 0.2f;

    /// <summary>Splits a generated meal's energy and macros across its ingredients.</summary>
    public static IReadOnlyList<GeneratedMealIngredientPortion> Calculate(
        IReadOnlyList<GeneratedMealIngredientProfile> ingredients,
        float targetCalories,
        float targetFats)
    {
        if (ingredients is null || ingredients.Count == 0 ||
            !float.IsFinite(targetCalories) || targetCalories <= 0f ||
            !float.IsFinite(targetFats) || targetFats < 0f ||
            ingredients.Any(ingredient => ingredient is null ||
                string.IsNullOrWhiteSpace(ingredient.Name) ||
                !IsValid(ingredient.ReferenceGrams, positive: true) ||
                !IsValid(ingredient.CaloriesPer100Grams, positive: false) ||
                !IsValid(ingredient.ProteinPer100Grams, positive: false) ||
                !IsValid(ingredient.CarbsPer100Grams, positive: false) ||
                !IsValid(ingredient.FatsPer100Grams, positive: false)))
            return [];

        var nonOilCalories = ingredients.Where(ingredient => !ingredient.IsOil)
            .Sum(ingredient => ingredient.CaloriesPer100Grams * ingredient.ReferenceGrams / NutrientReferenceGrams);
        if (!float.IsFinite(nonOilCalories) || nonOilCalories <= 0f)
            return [];

        var referenceCalories = ingredients.Sum(ingredient =>
            ingredient.CaloriesPer100Grams * ingredient.ReferenceGrams / NutrientReferenceGrams);
        if (!float.IsFinite(referenceCalories) || referenceCalories <= 0f)
            return [];

        var initialScale = targetCalories / referenceCalories;
        var oilIngredients = ingredients.Where(ingredient => ingredient.IsOil).ToArray();
        var requestedOilGrams = oilIngredients.Sum(ingredient => ingredient.ReferenceGrams * initialScale);
        var requestedOilCalories = oilIngredients.Sum(ingredient =>
            ingredient.CaloriesPer100Grams * ingredient.ReferenceGrams * initialScale / NutrientReferenceGrams);
        var oilGramLimit = MathF.Min(PortionCalculationRule.MaxOilGrams, targetFats);
        var oilGramScale = requestedOilGrams > 0f
            ? MathF.Min(1f, oilGramLimit / requestedOilGrams)
            : 0f;
        var oilCalorieScale = requestedOilCalories > 0f
            ? MathF.Min(1f, targetCalories * MaximumOilCalorieShare / requestedOilCalories)
            : 0f;
        var oilScale = initialScale * MathF.Min(oilGramScale, oilCalorieScale);
        var oilCalories = oilIngredients.Sum(ingredient =>
            ingredient.CaloriesPer100Grams * ingredient.ReferenceGrams * oilScale / NutrientReferenceGrams);
        var nonOilScale = (targetCalories - oilCalories) / nonOilCalories;
        if (!float.IsFinite(nonOilScale) || nonOilScale <= 0f)
            return [];

        return ingredients.Select(ingredient =>
        {
            var grams = ingredient.ReferenceGrams * (ingredient.IsOil ? oilScale : nonOilScale);
            return new GeneratedMealIngredientPortion(
                ingredient,
                grams,
                MacroNutrients.FromPer100g(
                    ingredient.CaloriesPer100Grams,
                    ingredient.ProteinPer100Grams,
                    ingredient.CarbsPer100Grams,
                    ingredient.FatsPer100Grams,
                    grams));
        }).Where(portion => portion.Grams > 0f).ToArray();
    }

    /// <summary>True when a portion value is finite and inside its allowed range.</summary>
    private static bool IsValid(float value, bool positive) =>
        float.IsFinite(value) && (positive ? value > 0f : value >= 0f);
}
