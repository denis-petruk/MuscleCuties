using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services.Nutrition.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class SuggestedMealPortionAdapterTests
{
    private const float NoNutrient = 0f;
    private const float EqualShare = 1f;
    private const float ChickenCaloriesPer100g = 165f;
    private const float ChickenProteinPer100g = 31f;
    private const float ChickenFatPer100g = 3.6f;
    private const float RiceCaloriesPer100g = 360f;
    private const float RiceProteinPer100g = 10f;
    private const float RiceCarbsPer100g = 75f;
    private const float RiceFatPer100g = 3f;
    private const float BroccoliCaloriesPer100g = 35f;
    private const float BroccoliProteinPer100g = 2.4f;
    private const float BroccoliCarbsPer100g = 7.2f;
    private const float BroccoliFatPer100g = 0.4f;
    private const float BroccoliServingGrams = 50f;
    private const float OilCaloriesPer100g = 884f;
    private const float OilFatPer100g = 100f;
    private const float LargeOilServingGrams = 30f;
    private const float RequestedOilGrams = 10f;
    private const float MealCalories = 600f;
    private const float NoFatMealCalories = 500f;
    private const float MealProtein = 35f;
    private const float MealCarbs = 65f;
    private const float MealFat = 35f;
    private const int MacroPrecision = 3;

    private static FoodItem Food(string name, float calories, float protein, float carbs, float fats,
        float serving = NoNutrient) => new()
    {
        Name = name, Calories = calories, Protein = protein, Carbs = carbs, Fats = fats,
        ServingSize = serving, ServingSizeUnit = "g"
    };

    private static FoodItem Chicken() => Food("Chicken", ChickenCaloriesPer100g,
        ChickenProteinPer100g, NoNutrient, ChickenFatPer100g);
    private static FoodItem Rice() => Food("Rice", RiceCaloriesPer100g,
        RiceProteinPer100g, RiceCarbsPer100g, RiceFatPer100g);
    private static FoodItem Broccoli() => Food("Broccoli", BroccoliCaloriesPer100g,
        BroccoliProteinPer100g, BroccoliCarbsPer100g, BroccoliFatPer100g, BroccoliServingGrams);
    private static FoodItem Oil(string name) => Food(name, OilCaloriesPer100g,
        NoNutrient, NoNutrient, OilFatPer100g, LargeOilServingGrams);

    [Fact]
    public void ServingSnapDoesNotCreateOilFromZeroOrExceedCap()
    {
        var oil = Oil("Oil");
        Assert.Equal(NoNutrient, PortionSolver.SnapToServing(oil, NoNutrient));
        Assert.Equal(PortionCalculationRule.MaxOilGrams, PortionSolver.SnapToServing(oil, RequestedOilGrams));
    }

    [Fact]
    public void MultipleOilsShareCapAndReportedMacrosMatchIngredients()
    {
        var entries = new SlotFoodMap(
            [new(Chicken(), EqualShare)], [new(Rice(), EqualShare)], [new(Broccoli(), EqualShare)],
            [new(Oil("Olive oil"), EqualShare), new(Oil("Sesame oil"), EqualShare)]);

        var solution = SuggestedMealService.SolveMultiIngredient(
            new MealNutritionTarget(MealType.Lunch, MealCalories, MealProtein, MealCarbs, MealFat),
            entries, UserGoal.MuscleTone, MealStyle.Plated, relaxed: true);

        Assert.NotNull(solution);
        Assert.NotNull(solution.SauceComponent);
        Assert.InRange(solution.SauceComponent.TotalGrams, NoNutrient, PortionCalculationRule.MaxOilGrams);
        Assert.True(solution.SauceComponent.TotalGrams > NoNutrient);
        var components = new[] { solution.ProteinComponent, solution.CarbComponent,
            solution.VitaminComponent, solution.SauceComponent };
        Assert.Equal(components.Sum(c => c.Macros.Calories), solution.Total.Calories, MacroPrecision);
        Assert.Equal(components.Sum(c => c.Macros.Protein), solution.Total.Protein, MacroPrecision);
        Assert.Equal(components.Sum(c => c.Macros.Carbs), solution.Total.Carbs, MacroPrecision);
        Assert.Equal(components.Sum(c => c.Macros.Fats), solution.Total.Fats, MacroPrecision);
    }

    [Fact]
    public void ZeroFatTargetDoesNotIntroduceOilDuringDistribution()
    {
        var oil = Oil("Oil");
        var entries = new SlotFoodMap(
            [new(Chicken(), EqualShare), new(oil, EqualShare)], [new(Rice(), EqualShare)],
            [new(Broccoli(), EqualShare)], null);
        var solution = SuggestedMealService.SolveMultiIngredient(
            new MealNutritionTarget(MealType.Lunch, NoFatMealCalories, MealProtein, MealCarbs, NoNutrient),
            entries, UserGoal.MuscleTone, MealStyle.Plated, relaxed: true);

        Assert.NotNull(solution);
        Assert.DoesNotContain(solution.ProteinComponent.Ingredients, ingredient => ingredient.Food == oil);
    }
}
