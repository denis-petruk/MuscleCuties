using MuscleCuties.Core.Domain.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class GeneratedMealPortionRuleTests
{
    private const float TargetCalories = 600f;
    private const float TargetFats = 20f;
    private const float CalorieTolerance = 0.001f;

    [Fact]
    public void UsesFoodSpecificDensitiesAndRecipePortions()
    {
        GeneratedMealIngredientProfile[] recipe =
        [
            new("Turkey", 145f, 149f, 19.5f, 0f, 7.7f),
            new("Beans", 95f, 132f, 8.9f, 23.7f, 0.5f),
            new("Tomatoes", 80f, 18f, 0.9f, 3.9f, 0.2f),
            new("Rice", 125f, 123f, 2.7f, 25.6f, 1f)
        ];

        var portions = GeneratedMealPortionRule.Calculate(recipe, TargetCalories, TargetFats);

        Assert.Equal(recipe.Length, portions.Count);
        Assert.Equal(recipe.Select(ingredient => ingredient.Name), portions.Select(portion => portion.Profile.Name));
        Assert.NotEqual(portions[0].Grams, portions[1].Grams);
        Assert.NotEqual(portions[1].Nutrients.Protein, portions[2].Nutrients.Protein);
        Assert.InRange(portions.Sum(portion => portion.Nutrients.Calories),
            TargetCalories - CalorieTolerance, TargetCalories + CalorieTolerance);
    }

    [Fact]
    public void InvalidNutrientReferenceProducesNoMeal()
    {
        GeneratedMealIngredientProfile[] recipe = [new("Unknown", 100f, float.NaN, 0f, 0f, 0f)];

        Assert.Empty(GeneratedMealPortionRule.Calculate(recipe, TargetCalories, TargetFats));
    }

    [Fact]
    public void OilRespectsFatBudgetWithoutChangingItsNutrientDensity()
    {
        GeneratedMealIngredientProfile[] recipe =
        [
            new("Rice", 125f, 123f, 2.7f, 25.6f, 1f),
            new("Olive Oil", 20f, 884f, 0f, 0f, 100f, true)
        ];

        var portions = GeneratedMealPortionRule.Calculate(recipe, TargetCalories, TargetFats);
        var oil = Assert.Single(portions, portion => portion.Profile.IsOil);

        Assert.InRange(oil.Grams, 0f, PortionCalculationRule.MaxOilGrams);
        Assert.Equal(884f, oil.Profile.CaloriesPer100Grams);
        Assert.InRange(portions.Sum(portion => portion.Nutrients.Calories),
            TargetCalories - CalorieTolerance, TargetCalories + CalorieTolerance);
    }
}
