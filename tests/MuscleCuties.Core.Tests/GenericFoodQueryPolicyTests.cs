using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.Tests;

public sealed class GenericFoodQueryPolicyTests
{
    [Theory]
    [InlineData("chicken breast", "chicken breast")]
    [InlineData("  Low-Fat Yogurt  ", "low fat yogurt")]
    [InlineData("olive oil", "olive oil")]
    [InlineData("sweet potato", "sweet potato")]
    [InlineData("brown rice", "brown rice")]
    [InlineData("cottage cheese", "cottage cheese")]
    [InlineData("salmon, raw", "salmon raw")]
    public void Recognized_Food_Names_Can_Be_Sent(string input, string expected)
    {
        Assert.Equal(expected, GenericFoodQueryPolicy.NormalizeOrEmpty(input));
    }

    [Theory]
    [InlineData("diabetes chicken")]
    [InlineData("Mary chicken")]
    [InlineData("my weight is 60 kg chicken")]
    [InlineData("knee pain salmon")]
    [InlineData("chicken breast (for PCOS)")]
    [InlineData("period")]
    [InlineData("raw")]
    [InlineData("chicken%20knee")]
    [InlineData("chicken 2026")]
    [InlineData("chicken 🩸")]
    [InlineData("")]
    public void Unknown_Or_Personal_Text_Fails_Closed(string input)
    {
        Assert.Equal(string.Empty, GenericFoodQueryPolicy.NormalizeOrEmpty(input));
    }

    [Fact]
    public void Long_Query_Fails_Closed_Without_Truncating_The_Private_Portion()
    {
        var input = "chicken " + new string('a', 80) + " injury";

        Assert.Equal(string.Empty, GenericFoodQueryPolicy.NormalizeOrEmpty(input));
    }
}
