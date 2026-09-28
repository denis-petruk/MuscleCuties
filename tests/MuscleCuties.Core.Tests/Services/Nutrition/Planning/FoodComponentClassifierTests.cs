using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests.Services.Nutrition.Planning;

public class FoodComponentClassifierTests
{
    private static FoodItem MakeFood(string name, float calories, float protein, float carbs, float fats)
        => new() { Id = 1, Name = name, Calories = calories, Protein = protein, Carbs = carbs, Fats = fats };

    [Theory]
    [InlineData("Hamburger Bun")]
    [InlineData("Whole Wheat Bun")]
    [InlineData("Sesame Seed Bun")]
    public void Burger_CarbBase_Accepts_Bun(string foodName)
    {
        var food = MakeFood(foodName, 140f, 4f, 26f, 2f);
        var burgerTerms = new[] { "hamburger bun", "bun" };

        Assert.True(FoodComponentClassifier.IsCarbSemanticMatch(food, burgerTerms));
    }

    [Theory]
    [InlineData("Rolled Oats")]
    [InlineData("Quick Oats")]
    [InlineData("Brown Rice")]
    [InlineData("Quinoa")]
    [InlineData("Sweet Potato")]
    [InlineData("Flour Tortilla")]
    public void Burger_CarbBase_Rejects_NonBread(string foodName)
    {
        var food = MakeFood(foodName, 150f, 5f, 27f, 2f);
        var burgerTerms = new[] { "hamburger bun", "bun" };

        Assert.False(FoodComponentClassifier.IsCarbSemanticMatch(food, burgerTerms));
    }

    [Theory]
    [InlineData("Oats")]
    [InlineData("Rolled Oats")]
    [InlineData("Gluten-Free Rolled Oats")]
    public void OatBowl_CarbBase_Accepts_Oats(string foodName)
    {
        var food = MakeFood(foodName, 150f, 5f, 27f, 3f);
        var oatTerms = new[] { "oats", "rolled oats", "gluten-free rolled oats" };

        Assert.True(FoodComponentClassifier.IsCarbSemanticMatch(food, oatTerms));
    }

    [Theory]
    [InlineData("Hamburger Bun")]
    [InlineData("Whole Wheat Bread")]
    [InlineData("Brown Rice")]
    public void OatBowl_CarbBase_Rejects_NonOat(string foodName)
    {
        var food = MakeFood(foodName, 140f, 4f, 26f, 2f);
        var oatTerms = new[] { "oats", "rolled oats", "gluten-free rolled oats" };

        Assert.False(FoodComponentClassifier.IsCarbSemanticMatch(food, oatTerms));
    }

    [Theory]
    [InlineData("Whole Wheat Bread")]
    [InlineData("Sourdough Bread")]
    public void Bread_Family_Matches_Bread_Variants(string foodName)
    {
        var food = MakeFood(foodName, 130f, 4f, 24f, 1.5f);
        var breadTerms = new[] { "bread", "whole wheat bread" };

        Assert.True(FoodComponentClassifier.IsCarbSemanticMatch(food, breadTerms));
    }

    [Fact]
    public void Unknown_Preferred_Terms_Allow_Any_Carb()
    {
        var food = MakeFood("Barley", 120f, 3f, 25f, 1f);
        var unknownTerms = new[] { "barley" };

        Assert.True(FoodComponentClassifier.IsCarbSemanticMatch(food, unknownTerms));
    }

    [Theory]
    [InlineData("Apple cider vinegar", 21f, 0f, 0.9f, 0f)]
    [InlineData("White vinegar, distilled", 18f, 0f, 0.04f, 0f)]
    [InlineData("Hot sauce, generic", 11f, 0.5f, 1.8f, 0.4f)]
    [InlineData("Ketchup, zero sugar", 15f, 0.2f, 3.5f, 0.1f)]
    [InlineData("Salsa, no sugar added", 29f, 1.5f, 5.5f, 0.2f)]
    [InlineData("Yellow mustard", 60f, 3.7f, 5.3f, 3.3f)]
    public void Classify_ZeroCalorie_Condiments_As_Sauce(
        string name, float cal, float protein, float carbs, float fats)
    {
        var food = MakeFood(name, cal, protein, carbs, fats);

        var result = FoodComponentClassifier.Classify(food);

        Assert.Equal(Models.Enums.Nutrition.ComponentType.Sauce, result);
    }

    [Theory]
    [InlineData("Apple cider vinegar", 21f, 0f, 0.9f, 0f)]
    [InlineData("Hot sauce, generic", 11f, 0.5f, 1.8f, 0.4f)]
    [InlineData("Ketchup, zero sugar", 15f, 0.2f, 3.5f, 0.1f)]
    [InlineData("Salsa, no sugar added", 29f, 1.5f, 5.5f, 0.2f)]
    public void IsZeroCalorieCondiment_Returns_True_For_Low_Cal_Condiments(
        string name, float cal, float protein, float carbs, float fats)
    {
        var food = MakeFood(name, cal, protein, carbs, fats);

        Assert.True(FoodComponentClassifier.IsZeroCalorieCondiment(food));
    }

    [Theory]
    [InlineData("Olive oil", 884f, 0f, 0f, 100f)]
    [InlineData("Sour cream, reduced fat", 135f, 3.5f, 5f, 11.5f)]
    [InlineData("Avocado, raw", 160f, 2f, 8.5f, 14.7f)]
    public void IsZeroCalorieCondiment_Returns_False_For_High_Cal_Items(
        string name, float cal, float protein, float carbs, float fats)
    {
        var food = MakeFood(name, cal, protein, carbs, fats);

        Assert.False(FoodComponentClassifier.IsZeroCalorieCondiment(food));
    }

    [Fact]
    public void Classify_Vinegar_Not_As_Vegetable()
    {
        var food = MakeFood("Apple cider vinegar", 21f, 0f, 0.9f, 0f);

        var result = FoodComponentClassifier.Classify(food);

        Assert.NotEqual(Models.Enums.Nutrition.ComponentType.Vegetable, result);
    }
}
