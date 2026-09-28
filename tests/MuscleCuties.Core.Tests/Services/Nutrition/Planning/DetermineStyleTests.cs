using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests.Services.Nutrition.Planning;

public class DetermineStyleTests
{
    private static FoodItem Food(string name, float protein = 20f, float carbs = 10f, float fats = 5f)
        => new()
        {
            Id = 1,
            Name = name,
            Calories = protein * 4f + carbs * 4f + fats * 9f,
            Protein = protein,
            Carbs = carbs,
            Fats = fats
        };

    // ── Handheld (bun / bread / toast) ──────────────────────────

    [Theory]
    [InlineData("Hamburger Bun")]
    [InlineData("Whole Wheat Bun")]
    [InlineData("Sesame Seed Bun")]
    [InlineData("Brioche Bun")]
    public void Burger_Bun_Returns_Handheld(string carbName)
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Ground Turkey", protein: 25f),
            Food(carbName, protein: 4f, carbs: 26f, fats: 2f),
            Food("Lettuce"),
            Food("Greek Yogurt", protein: 10f, fats: 3f));

        Assert.Equal(MealStyle.Handheld, result);
    }

    [Theory]
    [InlineData("Whole Wheat Bread")]
    [InlineData("Sourdough Bread")]
    public void Sandwich_Bread_Returns_Handheld(string carbName)
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Turkey Ham"),
            Food(carbName, protein: 4f, carbs: 22f, fats: 1f),
            Food("Tomato", protein: 1f, carbs: 4f, fats: 0f),
            Food("Mustard", protein: 0f, carbs: 1f, fats: 0f));

        Assert.Equal(MealStyle.Handheld, result);
    }

    [Fact]
    public void Avocado_Toast_Returns_Handheld()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Eggs", protein: 13f, carbs: 1f, fats: 10f),
            Food("Whole Wheat Bread", protein: 4f, carbs: 22f, fats: 1f),
            Food("Tomato", protein: 1f, carbs: 4f, fats: 0f),
            Food("Avocado", protein: 2f, carbs: 9f, fats: 15f));

        Assert.Equal(MealStyle.Handheld, result);
    }

    // ── Wrapped (tortilla / pita / flatbread) ───────────────────

    [Theory]
    [InlineData("Flour Tortilla")]
    [InlineData("Whole Wheat Tortilla")]
    [InlineData("Corn Tortilla")]
    public void Taco_Tortilla_Returns_Wrapped(string carbName)
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Ground Turkey", protein: 25f),
            Food(carbName, protein: 3f, carbs: 20f, fats: 2f),
            Food("Onion", protein: 1f, carbs: 9f, fats: 0f),
            Food("Sour Cream", protein: 2f, carbs: 3f, fats: 12f));

        Assert.Equal(MealStyle.Wrapped, result);
    }

    [Fact]
    public void Protein_Wrap_Returns_Wrapped()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken", protein: 31f, carbs: 0f, fats: 3f),
            Food("Flour Tortilla", protein: 3f, carbs: 20f, fats: 2f),
            Food("Lettuce"),
            Food("Greek Yogurt", protein: 10f, fats: 3f));

        Assert.Equal(MealStyle.Wrapped, result);
    }

    [Fact]
    public void Pita_Returns_Wrapped()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken"),
            Food("Pita Bread", protein: 5f, carbs: 33f, fats: 1f),
            Food("Tomato", protein: 1f, carbs: 4f, fats: 0f),
            Food("Hummus", protein: 8f, carbs: 14f, fats: 10f));

        Assert.Equal(MealStyle.Wrapped, result);
    }

    // ── Bowl ────────────────────────────────────────────────────

    [Fact]
    public void Rice_Bowl_Returns_Bowl()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken"),
            Food("Brown Rice", protein: 3f, carbs: 23f, fats: 1f),
            Food("Spinach"),
            Food("Soy Sauce", protein: 1f, carbs: 1f, fats: 0f));

        Assert.Equal(MealStyle.Bowl, result);
    }

    [Fact]
    public void Quinoa_Bowl_Returns_Bowl()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Tofu", protein: 8f, carbs: 2f, fats: 5f),
            Food("Quinoa", protein: 4f, carbs: 21f, fats: 2f),
            Food("Bell Pepper"),
            null);

        Assert.Equal(MealStyle.Bowl, result);
    }

    // ── Complex ─────────────────────────────────────────────────

    [Fact]
    public void Curry_With_Sauce_Returns_Complex()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Tofu", protein: 8f, carbs: 2f, fats: 5f),
            Food("Lentils", protein: 9f, carbs: 20f, fats: 0.4f),
            Food("Spinach"),
            Food("Coconut Curry Sauce", protein: 1f, carbs: 5f, fats: 12f));

        Assert.Equal(MealStyle.Complex, result);
    }

    // ── Plated ──────────────────────────────────────────────────

    [Fact]
    public void Salmon_Sweet_Potato_Returns_Plated()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Salmon", protein: 20f, carbs: 0f, fats: 13f),
            Food("Sweet Potato", protein: 2f, carbs: 20f, fats: 0f),
            Food("Kale"),
            Food("Olive Oil", protein: 0f, carbs: 0f, fats: 14f));

        Assert.Equal(MealStyle.Plated, result);
    }

    // ── Null sauce safety ───────────────────────────────────────

    [Fact]
    public void Null_Sauce_Burger_Still_Returns_Handheld()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Ground Beef", protein: 26f),
            Food("Hamburger Bun", protein: 4f, carbs: 26f, fats: 2f),
            Food("Lettuce"),
            null);

        Assert.Equal(MealStyle.Handheld, result);
    }

    [Fact]
    public void Null_Sauce_Wrap_Still_Returns_Wrapped()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken"),
            Food("Flour Tortilla", protein: 3f, carbs: 20f, fats: 2f),
            Food("Tomato", protein: 1f, carbs: 4f, fats: 0f),
            null);

        Assert.Equal(MealStyle.Wrapped, result);
    }

    [Fact]
    public void Null_Sauce_Plain_Protein_Returns_Plated()
    {
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken"),
            Food("Sweet Potato", protein: 2f, carbs: 20f, fats: 0f),
            Food("Broccoli"),
            null);

        Assert.Equal(MealStyle.Plated, result);
    }

    // ── False-positive prevention ───────────────────────────────

    [Fact]
    public void Sesame_Seed_Bun_Does_Not_Return_Bowl()
    {
        // "sesame" matches AsianTerms, but the bun vessel takes priority
        var result = MealComponentScorer.DetermineStyle(
            Food("Ground Turkey", protein: 25f),
            Food("Sesame Seed Bun", protein: 4f, carbs: 26f, fats: 2f),
            Food("Lettuce"),
            Food("Ketchup", protein: 0f, carbs: 5f, fats: 0f));

        Assert.Equal(MealStyle.Handheld, result);
        Assert.NotEqual(MealStyle.Bowl, result);
    }

    [Fact]
    public void Rice_Flour_Tortilla_Returns_Wrapped_Not_Bowl()
    {
        // "rice" matches BowlTerms, but tortilla vessel takes priority
        var result = MealComponentScorer.DetermineStyle(
            Food("Chicken"),
            Food("Rice Flour Tortilla", protein: 2f, carbs: 22f, fats: 1f),
            Food("Bell Pepper"),
            Food("Salsa", protein: 1f, carbs: 4f, fats: 0f));

        Assert.Equal(MealStyle.Wrapped, result);
    }
}
