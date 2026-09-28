using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services.Nutrition.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests.Services.Nutrition.Planning;

public class ZeroCalorieCondimentTests
{
    private static FoodItem MakeFood(int id, string name, float cal, float protein, float carbs, float fats)
        => new() { Id = id, Name = name, Calories = cal, Protein = protein, Carbs = carbs, Fats = fats };

    [Fact]
    public void SuggestedMeal_SauceComponent_Contains_Ingredient_When_Condiment_Available()
    {
        var condiment = MakeFood(99, "Hot sauce, generic", 11f, 0.5f, 1.8f, 0.4f);

        Assert.True(FoodComponentClassifier.IsZeroCalorieCondiment(condiment),
            "Hot sauce should qualify as a zero-calorie condiment.");

        var sauceIngredient = new Ingredient(condiment, 10f);
        var sauceComponent = new MealComponent(MealConceptSlotType.Sauce, [sauceIngredient]);

        Assert.NotNull(sauceComponent);
        Assert.Single(sauceComponent.Ingredients);
        Assert.True(sauceComponent.TotalCalories < 5f,
            "A 10g serving of hot sauce should add under 5 calories.");
    }

    [Fact]
    public void SuggestedMeal_AllComponents_Include_Sauce_When_Present()
    {
        var protein = MakeFood(1, "Chicken breast", 120f, 22.5f, 0f, 2.6f);
        var carb = MakeFood(2, "Brown rice", 123f, 2.7f, 25.6f, 1f);
        var veggie = MakeFood(3, "Spinach", 23f, 2.9f, 3.6f, 0.4f);
        var condiment = MakeFood(4, "Apple cider vinegar", 21f, 0f, 0.9f, 0f);

        var proteinComponent = new MealComponent(MealConceptSlotType.ProteinBase, [new Ingredient(protein, 150f)]);
        var carbComponent = new MealComponent(MealConceptSlotType.CarbBase, [new Ingredient(carb, 180f)]);
        var vitaminComponent = new MealComponent(MealConceptSlotType.VitaminBase, [new Ingredient(veggie, 100f)]);
        var sauceComponent = new MealComponent(MealConceptSlotType.Sauce, [new Ingredient(condiment, 10f)]);
        var macros = MacroNutrients.Sum([
            proteinComponent.Macros, carbComponent.Macros,
            vitaminComponent.Macros, sauceComponent.Macros
        ]);

        var meal = new SuggestedMeal(
            "Test Bowl", "A test meal with condiment.",
            carbComponent, proteinComponent, vitaminComponent,
            sauceComponent, macros, 0.85f, MealStyle.Bowl,
            [new SpiceBlendOption("Garlic + pepper", "Simple seasoning.")]);

        Assert.NotNull(meal.SauceComponent);
        Assert.True(meal.SauceComponent.Ingredients.Count >= 1,
            "Sauce component must contain at least one ingredient.");
        Assert.Contains(meal.AllComponents, c => c.Type == MealConceptSlotType.Sauce);
    }

    [Fact]
    public void SuggestedMeal_Sauce_Ingredient_Is_Zero_Calorie_Condiment()
    {
        var condiment = MakeFood(10, "Salsa, no sugar added", 29f, 1.5f, 5.5f, 0.2f);
        var sauceComponent = new MealComponent(MealConceptSlotType.Sauce, [new Ingredient(condiment, 10f)]);

        Assert.True(
            sauceComponent.Ingredients.Any(i => FoodComponentClassifier.IsZeroCalorieCondiment(i.Food)),
            "Sauce component must include at least one zero-calorie condiment.");
    }

    [Fact]
    public void PortionSolution_With_Sauce_Has_NonNull_SauceComponent()
    {
        var protein = MakeFood(1, "Chicken breast", 120f, 22.5f, 0f, 2.6f);
        var carb = MakeFood(2, "Brown rice", 123f, 2.7f, 25.6f, 1f);
        var veggie = MakeFood(3, "Spinach", 23f, 2.9f, 3.6f, 0.4f);
        var condiment = MakeFood(4, "White vinegar, distilled", 18f, 0f, 0.04f, 0f);

        var proteinComp = new MealComponent(MealConceptSlotType.ProteinBase, [new Ingredient(protein, 150f)]);
        var carbComp = new MealComponent(MealConceptSlotType.CarbBase, [new Ingredient(carb, 180f)]);
        var vitaminComp = new MealComponent(MealConceptSlotType.VitaminBase, [new Ingredient(veggie, 100f)]);
        var sauceComp = new MealComponent(MealConceptSlotType.Sauce, [new Ingredient(condiment, 10f)]);
        var total = MacroNutrients.Sum([proteinComp.Macros, carbComp.Macros, vitaminComp.Macros, sauceComp.Macros]);

        var solution = new PortionSolution(carbComp, proteinComp, vitaminComp, sauceComp, total);

        Assert.NotNull(solution.SauceComponent);
        Assert.True(solution.SauceComponent.Ingredients.Count >= 1);
        Assert.True(solution.SauceComponent.TotalCalories < 5f,
            "Zero-calorie condiment at 10g should contribute under 5 calories.");
    }
}
