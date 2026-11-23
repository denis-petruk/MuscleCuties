using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Tests;

public sealed class FoodSearchNormalizationTests
{
    [Fact]
    public void Normalisation_Folds_Case_Punctuation_And_Plurals()
    {
        Assert.Equal("greek yoghurt", FoodCatalogRankingRule.NormalizeSearchText("Greek  Yoghurts!"));
    }

    [Fact]
    public void Tokenisation_Drops_Stop_Words_And_Single_Letters()
    {
        var tokens = FoodCatalogRankingRule.Tokenize("a bowl of the lentils");

        Assert.Equal(["bowl", "lentil"], tokens);
    }

    [Fact]
    public void Digits_Survive_Normalisation()
    {
        Assert.Equal("vitamin b12", FoodCatalogRankingRule.NormalizeSearchText("Vitamin B12"));
    }

    [Fact]
    public void Blank_Input_Normalises_To_Empty()
    {
        Assert.Equal(string.Empty, FoodCatalogRankingRule.NormalizeSearchText("   "));
        Assert.Empty(FoodCatalogRankingRule.Tokenize(null!));
    }
}
