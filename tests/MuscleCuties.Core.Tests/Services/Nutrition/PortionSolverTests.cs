using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class PortionSolverTests
{
    private static readonly FoodItem Chicken = CreateFood("Chicken Breast", 165f, 31f, 0f, 3.6f);
    private static readonly FoodItem Rice = CreateFood("Brown Rice", 112f, 2.6f, 24f, 0.9f);
    private static readonly FoodItem Broccoli = CreateFood("Broccoli", 34f, 2.8f, 7f, 0.4f);
    private static readonly FoodItem OliveOil = CreateFood("Olive Oil", 884f, 0f, 0f, 100f, id: 4);

    private static readonly MealNutritionTarget LunchTarget = new(
        MealType.Lunch, 550f, 40f, 55f, 18f);

    [Fact]
    public void Solve_TypicalLunchCombo_ReturnsSolution()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        Assert.NotNull(result);
        Assert.NotNull(result.ProteinComponent);
        Assert.NotNull(result.CarbComponent);
        Assert.NotNull(result.VitaminComponent);
        Assert.NotNull(result.SauceComponent);
        Assert.True(result.Total.Protein > 0f);
        Assert.True(result.Total.Calories > 0f);
    }

    [Fact]
    public void Solve_WithoutSauce_ReturnsNullSauceComponent()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, sauce: null,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is not null)
            Assert.Null(result.SauceComponent);
    }

    [Fact]
    public void Solve_EachComponentHasIngredients()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is null)
            return;

        Assert.Single(result.ProteinComponent.Ingredients);
        Assert.Single(result.CarbComponent.Ingredients);
        Assert.Single(result.VitaminComponent.Ingredients);
        Assert.Single(result.SauceComponent!.Ingredients);
    }

    [Fact]
    public void Solve_ComponentsHaveCorrectSlotTypes()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is null)
            return;

        Assert.Equal(MealConceptSlotType.ProteinBase, result.ProteinComponent.Type);
        Assert.Equal(MealConceptSlotType.CarbBase, result.CarbComponent.Type);
        Assert.Equal(MealConceptSlotType.VitaminBase, result.VitaminComponent.Type);
        Assert.Equal(MealConceptSlotType.Sauce, result.SauceComponent!.Type);
    }

    [Fact]
    public void Solve_IngredientsRoundedToFive()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is null)
            return;

        foreach (var ingredient in result.ProteinComponent.Ingredients)
            Assert.Equal(0f, ingredient.Grams % 5f, 0.01f);

        foreach (var ingredient in result.CarbComponent.Ingredients)
            Assert.Equal(0f, ingredient.Grams % 5f, 0.01f);

        foreach (var ingredient in result.VitaminComponent.Ingredients)
            Assert.Equal(0f, ingredient.Grams % 5f, 0.01f);

        if (result.SauceComponent is not null)
            foreach (var ingredient in result.SauceComponent.Ingredients)
                Assert.Equal(0f, ingredient.Grams % 5f, 0.01f);
    }

    [Fact]
    public void Solve_IngredientMacrosScaledToGrams()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is null)
            return;

        var proteinIngredient = result.ProteinComponent.Ingredients[0];
        var expectedProtein = Chicken.Protein * proteinIngredient.Grams / 100f;

        Assert.Equal(expectedProtein, proteinIngredient.Protein, 0.01f);
    }

    [Fact]
    public void Solve_ProteinWithZeroProtein_ReturnsNull()
    {
        var noProtein = CreateFood("Empty", 100f, 0f, 10f, 5f);

        var result = PortionSolver.Solve(
            LunchTarget, noProtein, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false);

        Assert.Null(result);
    }

    [Fact]
    public void Solve_CarbWithZeroCarbs_ReturnsNull()
    {
        var noCarbs = CreateFood("No Carbs", 100f, 10f, 0f, 5f);

        var result = PortionSolver.Solve(
            LunchTarget, Chicken, noCarbs, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false);

        Assert.Null(result);
    }

    [Fact]
    public void Solve_RestDay_IncreasesVegPortion()
    {
        var restResult = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: true);

        var trainingResult = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false);

        if (restResult is null || trainingResult is null)
            return;

        Assert.True(restResult.VitaminComponent.TotalGrams >= trainingResult.VitaminComponent.TotalGrams);
    }

    [Fact]
    public void Solve_FatLossGoal_IncreasesVegPortion()
    {
        var fatLoss = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, null,
            UserGoal.FatLoss, isRestDay: false);

        var maintain = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false);

        if (fatLoss is null || maintain is null)
            return;

        Assert.True(fatLoss.VitaminComponent.TotalGrams >= maintain.VitaminComponent.TotalGrams);
    }

    [Fact]
    public void Solve_RelaxedMode_AcceptsWiderTolerance()
    {
        var tightTarget = new MealNutritionTarget(MealType.Lunch, 300f, 40f, 20f, 5f);

        var strict = PortionSolver.Solve(
            tightTarget, Chicken, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false, relaxed: false);

        var relaxed = PortionSolver.Solve(
            tightTarget, Chicken, Rice, Broccoli, null,
            UserGoal.MaintainHealth, isRestDay: false, relaxed: true);

        if (strict is not null)
            Assert.NotNull(relaxed);
    }

    [Fact]
    public void Solve_SauceGramsClampedMax30()
    {
        var highFatTarget = new MealNutritionTarget(MealType.Lunch, 800f, 40f, 60f, 50f);

        var result = PortionSolver.Solve(
            highFatTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result?.SauceComponent is null)
            return;

        Assert.True(result.SauceComponent.TotalGrams <= 30f);
    }

    [Fact]
    public void Solve_TotalMacrosMatchComponentSum()
    {
        var result = PortionSolver.Solve(
            LunchTarget, Chicken, Rice, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false);

        if (result is null)
            return;

        var sumCalories = result.ProteinComponent.TotalCalories +
                          result.CarbComponent.TotalCalories +
                          result.VitaminComponent.TotalCalories +
                          (result.SauceComponent?.TotalCalories ?? 0f);

        Assert.Equal(result.Total.Calories, sumCalories, 0.1f);
    }

    [Fact]
    public void SnapToServing_FoodWithServingSize_SnapsToNearestWholeServing()
    {
        var tortilla = CreateFood("Flour Tortilla", 150f, 4f, 26f, 3.5f);
        tortilla.ServingSize = 40f;
        tortilla.ServingSizeUnit = "g";

        Assert.Equal(40f, PortionSolver.SnapToServing(tortilla, 30f));
        Assert.Equal(40f, PortionSolver.SnapToServing(tortilla, 55f));
        Assert.Equal(80f, PortionSolver.SnapToServing(tortilla, 70f));
    }

    [Fact]
    public void SnapToServing_BreadAllowsMultipleServings()
    {
        var bread = CreateFood("Whole Wheat Bread", 80f, 4f, 14f, 1f);
        bread.ServingSize = 30f;
        bread.ServingSizeUnit = "g";

        Assert.Equal(90f, PortionSolver.SnapToServing(bread, 85f));
        Assert.Equal(120f, PortionSolver.SnapToServing(bread, 110f));
    }

    [Fact]
    public void SnapToServing_FoodWithoutServingSize_RoundsToFive()
    {
        var result = PortionSolver.SnapToServing(Rice, 127f);

        Assert.Equal(125f, result);
    }

    [Fact]
    public void SnapToServing_ServingSizeOf100g_TreatedAsBulk_RoundsToFive()
    {
        var bulk = CreateFood("Ground Chicken", 150f, 20f, 0f, 8f);
        bulk.ServingSize = 100f;
        bulk.ServingSizeUnit = "g";

        var result = PortionSolver.SnapToServing(bulk, 137f);

        Assert.Equal(135f, result);
    }

    [Fact]
    public void SnapToServing_EggSnapsToWholeEggs()
    {
        var egg = CreateFood("Egg", 155f, 13f, 1.1f, 11f);
        egg.ServingSize = 50f;
        egg.ServingSizeUnit = "g";

        Assert.Equal(50f, PortionSolver.SnapToServing(egg, 40f));
        Assert.Equal(100f, PortionSolver.SnapToServing(egg, 85f));
        Assert.Equal(150f, PortionSolver.SnapToServing(egg, 130f));
    }

    [Fact]
    public void SnapToServing_AlwaysReturnsAtLeastOneServing()
    {
        var tortilla = CreateFood("Flour Tortilla", 150f, 4f, 26f, 3.5f);
        tortilla.ServingSize = 40f;
        tortilla.ServingSizeUnit = "g";

        Assert.Equal(40f, PortionSolver.SnapToServing(tortilla, 5f));
    }

    [Fact]
    public void Solve_FoodWithServingSize_UsesWholeServings()
    {
        var tortilla = CreateFood("Flour Tortilla", 150f, 4f, 26f, 3.5f);
        tortilla.ServingSize = 45f;
        tortilla.ServingSizeUnit = "g";

        var result = PortionSolver.Solve(
            LunchTarget, Chicken, tortilla, Broccoli, OliveOil,
            UserGoal.MaintainHealth, isRestDay: false, relaxed: true);

        if (result is null)
            return;

        var carbGrams = result.CarbComponent.TotalGrams;
        Assert.Equal(0f, carbGrams % 45f, 0.01f);
    }

    private static FoodItem CreateFood(string name, float cal, float protein, float carbs, float fats, int id = 0)
    {
        return new FoodItem
        {
            Id = id,
            Name = name,
            Calories = cal,
            Protein = protein,
            Carbs = carbs,
            Fats = fats
        };
    }
}
