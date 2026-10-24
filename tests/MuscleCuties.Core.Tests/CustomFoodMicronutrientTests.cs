using MuscleCuties.Core.Domain.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CustomFoodMicronutrientTests
{
    private const float ServingGrams = 50f;
    private const float IronPerServingMilligrams = 2f;
    private const decimal ExpectedIronPerHundredGrams = 4m;
    private const float ServingAmount = 1f;
    private const float CaloriesPerServing = 100f;
    private const float ProteinPerServing = 10f;
    private const float CarbsPerServing = 15f;
    private const float FatsPerServing = 5f;

    [Fact]
    public void OptionalMicronutrientUsesTheSamePerHundredGramBasisAsMacros()
    {
        var valid = CustomFoodCreationRule.TryNormalizeOptionalMicronutrient(
            IronPerServingMilligrams, ServingGrams, out var normalized);

        Assert.True(valid);
        Assert.Equal(ExpectedIronPerHundredGrams, normalized);
        Assert.True(CustomFoodCreationRule.TryNormalizeOptionalMicronutrient(
            null, ServingGrams, out var unknown));
        Assert.Null(unknown);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-2f)]
    public void InvalidMicronutrientIsRejected(float input)
    {
        Assert.False(CustomFoodCreationRule.TryNormalizeOptionalMicronutrient(
            input, ServingGrams, out _));
    }

    [Fact]
    public void CustomFoodRequestUsesTheSharedMacroAndMicronutrientRules()
    {
        Assert.True(CustomFoodCreationRule.IsValidRequest(
            "Oats", ServingAmount, "g", CaloriesPerServing, ProteinPerServing,
            CarbsPerServing, FatsPerServing, IronPerServingMilligrams, null, null, null));
        Assert.False(CustomFoodCreationRule.IsValidRequest(
            "Oats", ServingAmount, "g", CaloriesPerServing, ProteinPerServing,
            CarbsPerServing, FatsPerServing, float.NaN, null, null, null));
    }
}
