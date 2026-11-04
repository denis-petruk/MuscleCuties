using MuscleCuties.Core.Domain.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class NutritionLogRuleTests
{
    private const int OneDayBeforeBoundary = -1;
    private const int OneDayAfterBoundary = 1;
    private const float ValidIngredientGrams = 100f;
    private const float SecondValidIngredientGrams = 50f;
    private const float ZeroIngredientGrams = 0f;

    [Fact]
    public void SupportedDateIncludesConfiguredBounds()
    {
        Assert.True(NutritionLogRule.IsSupportedDate(NutritionLogRule.MinimumSupportedDate));
        Assert.True(NutritionLogRule.IsSupportedDate(NutritionLogRule.MaximumSupportedDate));
        Assert.False(NutritionLogRule.IsSupportedDate(
            NutritionLogRule.MinimumSupportedDate.AddDays(OneDayBeforeBoundary)));
        Assert.False(NutritionLogRule.IsSupportedDate(
            NutritionLogRule.MaximumSupportedDate.AddDays(OneDayAfterBoundary)));
    }

    [Fact]
    public void MealRequiresTypeAndPositiveFiniteIngredientWeights()
    {
        var ingredients = new[] { ValidIngredientGrams, SecondValidIngredientGrams };

        Assert.True(NutritionLogRule.IsValidMeal("Lunch", ingredients));
        Assert.False(NutritionLogRule.IsValidMeal(" ", ingredients));
        Assert.False(NutritionLogRule.IsValidMeal("Lunch", []));
        Assert.False(NutritionLogRule.IsValidMeal("Lunch", [float.NaN]));
        Assert.False(NutritionLogRule.IsValidMeal("Lunch", [ZeroIngredientGrams]));
    }
}
