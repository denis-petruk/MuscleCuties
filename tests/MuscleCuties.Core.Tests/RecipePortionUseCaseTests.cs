using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class RecipePortionUseCaseTests
{
    private const float RecipeServingsCount = 2f;
    private const float PerServingGrams = 100f;
    private const float PerServingCalories = 200f;
    private const float PerServingProtein = 20f;
    private const float PerServingCarbs = 25f;
    private const float PerServingNetCarbs = 22f;
    private const float PerServingFats = 8f;
    private const float OneRequestedServing = 1f;
    private const string OunceUnit = "oz";
    private const float OneOunce = 1f;

    [Fact]
    public void PortionCalculationUsesCoreRuleAndReturnsOutputDto()
    {
        var request = new CalculateRecipePortionRequest(
            Guid.NewGuid(),
            "Example recipe",
            RecipeServingsCount,
            PerServingGrams,
            PerServingCalories,
            PerServingProtein,
            PerServingCarbs,
            PerServingNetCarbs,
            PerServingFats,
            OneRequestedServing,
            null);

        var result = new RecipePortionUseCase().Calculate(request);

        Assert.Equal(PerServingGrams * RecipeServingsCount, result.BatchRawWeightGrams);
        Assert.Equal(PerServingCalories * RecipeServingsCount, result.BatchCalories);
        Assert.Equal(OneRequestedServing, result.SelectedServingsCount);
        Assert.Equal(PerServingGrams, result.SelectedGrams);
        Assert.Equal(PerServingCalories, result.Calories);
    }

    [Fact]
    public void MassConversionReturnsCoreValidatedGrams()
    {
        var result = new RecipePortionUseCase().ConvertMassToGrams(OneOunce, OunceUnit);

        Assert.True(result.Succeeded);
        Assert.Equal(FoodPortionMassRule.GramsPerOunce, result.Grams);
    }
}
