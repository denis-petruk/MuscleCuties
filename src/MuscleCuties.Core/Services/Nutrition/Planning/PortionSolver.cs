using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class PortionSolver
{
    private const float KcalTolerancePct = 0.15f;
    private const float ProteinDeficitToleranceG = 8f;
    private const float RelaxedKcalTolerancePct = 0.25f;

    public static PortionSolution? Solve(
        MealNutritionTarget target,
        FoodItem protein,
        FoodItem carb,
        FoodItem veg,
        FoodItem? sauce,
        UserGoal goal,
        bool isRestDay,
        MealStyle style = MealStyle.Plated,
        bool relaxed = false)
    {
        if (protein.Protein <= 0f || carb.Carbs <= 0f)
            return null;

        var vegMultiplier = (isRestDay ? 1.5f : 1f) * (goal == UserGoal.FatLoss ? 1.3f : 1f);
        var vegGrams = Clamp(120f * vegMultiplier, 60f, 300f);
        var vegMacros = MacroNutrients.FromFood(veg, vegGrams);

        var remainingCarbG = MathF.Max(target.Carbs - vegMacros.Carbs, 0f);
        var carbGrams = carb.Carbs > 0f
            ? Clamp(remainingCarbG / carb.Carbs * 100f, 30f, 250f)
            : 100f;
        var carbMacros = MacroNutrients.FromFood(carb, carbGrams);

        var remainingProteinG = MathF.Max(target.Protein - vegMacros.Protein - carbMacros.Protein, 0f);
        var proteinGrams = Clamp(remainingProteinG / protein.Protein * 100f, 50f, 300f);

        float sauceGrams = 0f;
        if (sauce is not null && sauce.Fats > 0f)
        {
            var proteinMacros = MacroNutrients.FromFood(protein, proteinGrams);
            var currentFats = proteinMacros.Fats + carbMacros.Fats + vegMacros.Fats;
            var remainingFatG = MathF.Max(target.Fats - currentFats, 0f);
            sauceGrams = Clamp(remainingFatG / sauce.Fats * 100f, 0f, 30f);
        }

        var proteinIngredient = new Ingredient(protein, SnapToServing(protein, proteinGrams));
        var carbIngredient = new Ingredient(carb, SnapToServing(carb, carbGrams));
        var vegIngredient = new Ingredient(veg, SnapToServing(veg, vegGrams));

        var proteinComponent = new MealComponent(MealConceptSlotType.ProteinBase, [proteinIngredient]);
        var carbComponent = new MealComponent(MealConceptSlotType.CarbBase, [carbIngredient]);
        var vitaminComponent = new MealComponent(MealConceptSlotType.VitaminBase, [vegIngredient]);

        MealComponent? sauceComponent = null;
        if (sauce is not null && sauceGrams > 0f)
        {
            var sauceIngredient = new Ingredient(sauce, SnapToServing(sauce, sauceGrams));
            sauceComponent = new MealComponent(MealConceptSlotType.Sauce, [sauceIngredient]);
        }

        var allMacros = new List<MacroNutrients>
        {
            proteinIngredient.Macros,
            carbIngredient.Macros,
            vegIngredient.Macros
        };
        if (sauceComponent is not null)
            allMacros.Add(sauceComponent.Macros);

        var total = MacroNutrients.Sum(allMacros);

        var kcalTolerance = relaxed ? RelaxedKcalTolerancePct : KcalTolerancePct;
        if (target.Calories > 0f &&
            MathF.Abs(total.Calories - target.Calories) / target.Calories > kcalTolerance)
            return null;

        var proteinDeficit = target.Protein - total.Protein;
        if (proteinDeficit > ProteinDeficitToleranceG)
            return null;

        return new PortionSolution(carbComponent, proteinComponent, vitaminComponent, sauceComponent, total);
    }

    public static float SnapToServing(FoodItem food, float targetGrams)
    {
        var servingGrams = GetBaseServingGrams(food);
        if (servingGrams <= 0f)
            return RoundTo5(targetGrams);

        var servings = MathF.Max(1f, MathF.Round(targetGrams / servingGrams));
        return servings * servingGrams;
    }

    private static float GetBaseServingGrams(FoodItem food)
    {
        if (food.ServingSize is not > 0f)
            return 0f;

        if (!FoodServingOptions.TryConvertToGrams(
                food.ServingSize.Value, food.ServingSizeUnit, out var grams))
            return 0f;

        if (MathF.Abs(grams - 100f) < 1f)
            return 0f;

        return grams;
    }

    private static float Clamp(float value, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, value));
    }

    private static float RoundTo5(float grams)
    {
        return MathF.Round(grams / 5f) * 5f;
    }
}
