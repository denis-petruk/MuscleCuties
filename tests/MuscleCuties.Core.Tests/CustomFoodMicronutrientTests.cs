using MuscleCuties.Core.Domain.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CustomFoodMicronutrientTests
{
    private const float ServingGrams = 50f;
    private const float IronPerServingMilligrams = 2f;
    private const decimal ExpectedIronPerHundredGrams = 4m;

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
}
