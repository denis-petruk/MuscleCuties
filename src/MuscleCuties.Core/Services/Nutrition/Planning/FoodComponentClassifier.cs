using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class FoodComponentClassifier
{
    private const float MinimumProteinAnchorGrams = 8f;
    private const float MinimumProteinEnergyShare = 0.20f;
    private const float CarbDominanceRatio = 1.5f;
    private const float ZeroCalorieCondimentThreshold = 40f;

    private static readonly string[] CondimentTerms =
        ["vinegar", "mustard", "ketchup", "salsa", "sriracha", "soy sauce", "hot sauce", "fish sauce"];

    public static ComponentType Classify(FoodItem food)
    {
        var cal = food.Calories;
        var p = food.Protein;
        var c = food.Carbs;
        var f = food.Fats;

        if (IsSauce(cal, p, c, f))
            return ComponentType.Sauce;

        if (IsCondimentByName(food.Name))
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

    /// <summary>
    /// Checks whether a food item belongs to the same semantic carb family
    /// as the slot's preferred ingredient terms. Prevents cross-family fallback
    /// (e.g. oats landing in a Burger concept that requires bun/bread).
    /// </summary>
    public static bool IsCarbSemanticMatch(FoodItem food, IReadOnlyList<string> preferredTerms)
    {
        var family = FindCarbFamily(preferredTerms);
        if (family is null)
            return true;

        return family.Any(term =>
            food.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[][] CarbFamilies =
    [
        ["bread", "bun", "hamburger bun", "whole wheat bread", "toast"],
        ["tortilla", "flour tortilla", "wrap"],
        ["rice", "brown rice", "quinoa"],
        ["oats", "rolled oats", "gluten-free rolled oats", "chia seeds"],
        ["potato", "white potato", "sweet potato"],
        ["beans", "black beans", "pinto beans", "cannellini beans", "chickpeas", "lentils"],
        ["hummus"],
        ["blueberries", "strawberries", "raspberries"]
    ];

    private static string[]? FindCarbFamily(IReadOnlyList<string> preferredTerms)
    {
        foreach (var family in CarbFamilies)
        {
            if (preferredTerms.Any(term =>
                    family.Any(f => f.Equals(term, StringComparison.OrdinalIgnoreCase))))
                return family;
        }

        return null;
    }

    public static bool IsZeroCalorieCondiment(FoodItem food)
    {
        return food.Calories <= ZeroCalorieCondimentThreshold &&
               food.Carbs < 10f &&
               (IsCondimentByName(food.Name) ||
                food.Name.Contains("sauce", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCondimentByName(string name)
    {
        foreach (var term in CondimentTerms)
        {
            if (name.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

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
