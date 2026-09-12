using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class FoodComponentClassifierTests
{
    [Theory]
    [InlineData("Chicken Breast", 165f, 31f, 0f, 3.6f, ComponentType.Protein)]
    [InlineData("Salmon Fillet", 208f, 20f, 0f, 13f, ComponentType.Protein)]
    [InlineData("Eggs", 155f, 13f, 1.1f, 11f, ComponentType.Protein)]
    [InlineData("Greek Yogurt", 59f, 10f, 3.6f, 0.7f, ComponentType.Protein)]
    [InlineData("Tofu", 76f, 8f, 1.9f, 4.8f, ComponentType.Protein)]
    [InlineData("Tuna", 130f, 29f, 0f, 1f, ComponentType.Protein)]
    [InlineData("Turkey Breast", 135f, 30f, 0f, 1f, ComponentType.Protein)]
    [InlineData("Shrimp", 99f, 24f, 0.2f, 0.3f, ComponentType.Protein)]
    [InlineData("Cottage Cheese", 98f, 11f, 3.4f, 4.3f, ComponentType.Protein)]
    public void Classify_ProteinFoods_ReturnsProtein(
        string name, float cal, float protein, float carbs, float fats, ComponentType expected)
    {
        var food = CreateFood(name, cal, protein, carbs, fats);
        Assert.Equal(expected, FoodComponentClassifier.Classify(food));
    }

    [Theory]
    [InlineData("Brown Rice", 112f, 2.6f, 24f, 0.9f, ComponentType.Carb)]
    [InlineData("Sweet Potato", 86f, 1.6f, 20f, 0.1f, ComponentType.Carb)]
    [InlineData("Quinoa", 120f, 4.4f, 21f, 1.9f, ComponentType.Carb)]
    [InlineData("Oats", 389f, 13.2f, 66f, 6.9f, ComponentType.Carb)]
    [InlineData("Whole Wheat Pasta", 131f, 5.3f, 27f, 0.6f, ComponentType.Carb)]
    public void Classify_CarbFoods_ReturnsCarb(
        string name, float cal, float protein, float carbs, float fats, ComponentType expected)
    {
        var food = CreateFood(name, cal, protein, carbs, fats);
        Assert.Equal(expected, FoodComponentClassifier.Classify(food));
    }

    [Theory]
    [InlineData("Broccoli", 34f, 2.8f, 7f, 0.4f, ComponentType.Vegetable)]
    [InlineData("Spinach", 23f, 2.9f, 3.6f, 0.4f, ComponentType.Vegetable)]
    [InlineData("Kale", 49f, 4.3f, 9f, 0.9f, ComponentType.Vegetable)]
    [InlineData("Bell Pepper", 31f, 1f, 6f, 0.3f, ComponentType.Vegetable)]
    [InlineData("Zucchini", 17f, 1.2f, 3.1f, 0.3f, ComponentType.Vegetable)]
    [InlineData("Cucumber", 15f, 0.7f, 3.6f, 0.1f, ComponentType.Vegetable)]
    public void Classify_Vegetables_ReturnsVegetable(
        string name, float cal, float protein, float carbs, float fats, ComponentType expected)
    {
        var food = CreateFood(name, cal, protein, carbs, fats);
        Assert.Equal(expected, FoodComponentClassifier.Classify(food));
    }

    [Theory]
    [InlineData("Olive Oil", 884f, 0f, 0f, 100f, ComponentType.Sauce)]
    [InlineData("Soy Sauce", 53f, 8.1f, 4.9f, 0.04f, ComponentType.Mixed)]
    public void Classify_Sauces_ReturnsExpected(
        string name, float cal, float protein, float carbs, float fats, ComponentType expected)
    {
        var food = CreateFood(name, cal, protein, carbs, fats);
        Assert.Equal(expected, FoodComponentClassifier.Classify(food));
    }

    [Fact]
    public void Classify_Avocado_ReturnsSauceOrMixed()
    {
        var food = CreateFood("Avocado", 160f, 2f, 8.5f, 15f);
        var result = FoodComponentClassifier.Classify(food);
        Assert.True(result is ComponentType.Sauce or ComponentType.Mixed);
    }

    [Fact]
    public void Classify_ZeroCalorieFood_DoesNotThrow()
    {
        var food = CreateFood("Water", 0f, 0f, 0f, 0f);
        var result = FoodComponentClassifier.Classify(food);
        Assert.Equal(ComponentType.Vegetable, result);
    }

    private static FoodItem CreateFood(string name, float cal, float protein, float carbs, float fats)
    {
        return new FoodItem
        {
            Name = name,
            Calories = cal,
            Protein = protein,
            Carbs = carbs,
            Fats = fats
        };
    }
}
