using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class FoodComponentClassifier
{
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

        if (IsCarb(p, c))
            return ComponentType.Carb;

        if (IsProtein(cal, p))
            return ComponentType.Protein;

        return ComponentType.Mixed;
    }

    private static bool IsSauce(float cal, float protein, float carbs, float fats)
    {
        return cal > 40f && fats > 8f && protein < 5f && carbs < 20f;
    }

    private static bool IsProtein(float cal, float protein)
    {
        if (protein >= 15f)
            return true;

        return protein >= 10f || cal > 0f && protein * 4f / cal >= 0.4f;
    }

    private static bool IsVegetable(float cal, float carbs)
    {
        return cal < 50f && carbs < 10f;
    }

    private static bool IsCarb(float protein, float carbs)
    {
        return carbs >= 15f && protein < 15f;
    }
}
