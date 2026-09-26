using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class FoodComponentClassifier
{
    private const float MinimumProteinAnchorGrams = 8f;
    private const float MinimumProteinEnergyShare = 0.20f;
    private const float CarbDominanceRatio = 1.5f;

    public static ComponentType Classify(FoodItem food)
    {
        var cal = food.Calories;
        var p = food.Protein;
        var c = food.Carbs;
        var f = food.Fats;

        if (IsSauce(cal, p, c, f))
            return ComponentType.Sauce;

        if (food.Name.Contains("sauce", StringComparison.OrdinalIgnoreCase))
            return ComponentType.Mixed;

        if (IsVegetable(cal, c))
            return ComponentType.Vegetable;

        if (IsProteinAnchor(food))
            return ComponentType.Protein;

        if (IsCarb(p, c))
            return ComponentType.Carb;

        return ComponentType.Mixed;
    }

    public static bool IsProteinAnchor(FoodItem food)
    {
        if (!float.IsFinite(food.Calories) ||
            !float.IsFinite(food.Protein) ||
            !float.IsFinite(food.Carbs) ||
            !float.IsFinite(food.Fats) ||
            food.Calories <= 0f ||
            food.Protein < 0f ||
            food.Carbs < 0f ||
            food.Fats < 0f ||
            food.Protein < MinimumProteinAnchorGrams ||
            food.Name.Contains("sauce", StringComparison.OrdinalIgnoreCase))
            return false;

        var proteinEnergyShare = food.Protein * 4f / food.Calories;
        if (proteinEnergyShare < MinimumProteinEnergyShare)
            return false;

        return true;
    }

    public static bool IsCarbohydrateBase(FoodItem food)
        => IsCarb(food.Protein, food.Carbs) ||
           (IsProteinAnchor(food) && food.Carbs >= 15f);

    private static bool IsSauce(float cal, float protein, float carbs, float fats)
    {
        return cal > 40f && fats > 8f && protein < 5f && carbs < 20f;
    }

    private static bool IsVegetable(float cal, float carbs)
    {
        return cal < 50f && carbs < 10f;
    }

    private static bool IsCarb(float protein, float carbs)
    {
        return carbs >= 15f && IsCarbDominant(protein, carbs);
    }

    private static bool IsCarbDominant(float protein, float carbs)
    {
        return carbs > MathF.Max(protein, 1f) * CarbDominanceRatio;
    }
}
