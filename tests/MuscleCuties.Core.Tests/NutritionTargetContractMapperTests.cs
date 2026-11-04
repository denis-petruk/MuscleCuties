using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class NutritionTargetContractMapperTests
{
    private const float Calories = 2000f;
    private const float Protein = 120f;
    private const float Carbohydrates = 220f;
    private const float Fats = 60f;

    [Fact]
    public void MealDistributionIsMappedFromTheCoreRuleIntoOutputData()
    {
        var target = new NutritionTargetMacrosDto(Calories, Protein, Carbohydrates, Fats);
        var mapped = NutritionTargetContractMapper.WithMealDistribution(target, "Sweet");
        var expected = MealDistributionRule.GetShares(BreakfastPreference.Sweet);

        Assert.Equal(expected.Breakfast, mapped.MealDistribution?.Breakfast);
        Assert.Equal(expected.Lunch, mapped.MealDistribution?.Lunch);
        Assert.Equal(expected.Dinner, mapped.MealDistribution?.Dinner);
        Assert.Equal(expected.Snack, mapped.MealDistribution?.Snack);
        Assert.Equal(Calories, mapped.Calories);
    }

    [Fact]
    public void UnknownPreferenceMapsToTheSavouryCoreDefault()
    {
        var target = new NutritionTargetMacrosDto(Calories, Protein, Carbohydrates, Fats);
        var mapped = NutritionTargetContractMapper.WithMealDistribution(target, "NotAChoice");
        var expected = MealDistributionRule.GetShares(BreakfastPreference.Savoury);

        Assert.Equal(expected.Breakfast, mapped.MealDistribution?.Breakfast);
        Assert.Equal(expected.Lunch, mapped.MealDistribution?.Lunch);
        Assert.Equal(expected.Dinner, mapped.MealDistribution?.Dinner);
        Assert.Equal(expected.Snack, mapped.MealDistribution?.Snack);
    }
}
