using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class PortionSolver
{
    internal const float OilFatDensityThreshold = 80f;

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
        if (target is null || protein is null || carb is null || veg is null)
            return null;

        var proteinObs = new PortionObservation(protein.Calories, protein.Protein, protein.Carbs, protein.Fats,
            protein.ServingSize ?? 0f);
        var carbObs =
            new PortionObservation(carb.Calories, carb.Protein, carb.Carbs, carb.Fats, carb.ServingSize ?? 0f);
        var vegObs = new PortionObservation(veg.Calories, veg.Protein, veg.Carbs, veg.Fats, veg.ServingSize ?? 0f);
        var sauceObs = sauce is not null
            ? new PortionObservation(sauce.Calories, sauce.Protein, sauce.Carbs, sauce.Fats, sauce.ServingSize ?? 0f)
            : null;

        var res = PortionCalculationRule.Solve(
            target.Calories, target.Protein, target.Carbs, target.Fats,
            proteinObs, carbObs, vegObs, sauceObs,
            (int)goal, isRestDay, relaxed);

        if (res is null) return null;

        var carbComponent = new MealComponent(MealConceptSlotType.CarbBase, [new Ingredient(carb, res.CarbGrams)]);
        var proteinComponent =
            new MealComponent(MealConceptSlotType.ProteinBase, [new Ingredient(protein, res.ProteinGrams)]);
        var vitaminComponent = new MealComponent(MealConceptSlotType.VitaminBase, [new Ingredient(veg, res.VegGrams)]);
        var sauceComponent = sauce is not null && res.SauceGrams > 0f
            ? new MealComponent(MealConceptSlotType.Sauce, [new Ingredient(sauce, res.SauceGrams)])
            : null;

        var totalMacros = new MacroNutrients(res.TotalCalories, res.TotalProtein, res.TotalCarbs, res.TotalFats);

        return new PortionSolution(
            carbComponent,
            proteinComponent,
            vitaminComponent,
            sauceComponent,
            totalMacros
        );
    }

    public static float SnapToServing(FoodItem food, float targetGrams)
    {
        if (targetGrams <= 0f)
            return 0f;

        var servingGrams = GetBaseServingGrams(food);
        var grams = PortionCalculationRule.SnapToServing(targetGrams, servingGrams);
        return IsOil(food) ? MathF.Min(grams, PortionCalculationRule.MaxOilGrams) : grams;
    }

    internal static bool IsOil(FoodItem food) => food.Fats >= OilFatDensityThreshold;

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
}
