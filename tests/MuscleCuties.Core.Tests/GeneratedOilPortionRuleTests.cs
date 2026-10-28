using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Tests;

public sealed class GeneratedOilPortionRuleTests
{
    private const float LargePortionScale = 3f;
    private const float MealCalories = 600f;
    private const float TargetFats = 40f;
    private const float LowFatBudget = 2f;
    private const float LowCalorieBudget = 9f;
    private const int MultipleOils = 2;

    [Fact]
    public void MultipleOilsShareOneMealCap()
    {
        var grams = GeneratedOilPortionRule.CalculatePerIngredientGrams(
            LargePortionScale, MealCalories, TargetFats, MultipleOils);
        Assert.Equal(PortionCalculationRule.MaxOilGrams, grams * MultipleOils);
    }

    [Theory]
    [InlineData(MealCalories, 0f, 0f)]
    [InlineData(MealCalories, LowFatBudget, LowFatBudget)]
    [InlineData(LowCalorieBudget, TargetFats, 1f)]
    [InlineData(float.NaN, TargetFats, 0f)]
    [InlineData(MealCalories, float.PositiveInfinity, 0f)]
    [InlineData(-1f, TargetFats, 0f)]
    public void OilCannotExceedCalorieOrFatBudget(float calories, float fats, float expected)
    {
        Assert.Equal(expected, GeneratedOilPortionRule.CalculatePerIngredientGrams(
            LargePortionScale, calories, fats, 1));
    }

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(float.NaN, 1)]
    [InlineData(float.PositiveInfinity, 1)]
    [InlineData(1f, 0)]
    public void InvalidPortionHasNoOil(float scale, int count)
    {
        Assert.Equal(0f, GeneratedOilPortionRule.CalculatePerIngredientGrams(scale, MealCalories, TargetFats, count));
    }
}
