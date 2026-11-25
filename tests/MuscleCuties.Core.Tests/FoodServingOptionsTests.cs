using MuscleCuties.Core.Services.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class FoodServingOptionsTests
{
    [Fact]
    public void SharedServingOptionParseKeepsEveryUnitAndDropsUnusableRows()
    {
        const string json = """
            [{"label":"oz","unit":"oz","grams":28.35,"source":"Standard"},
             {"label":"g","unit":"g","grams":1,"source":"Standard"},
             {"label":"   ","unit":"g","grams":5,"source":"Standard"},
             {"label":"zero","unit":"g","grams":0,"source":"Standard"}]
            """;

        var options = FoodServingOptions.ReadOptions(json);

        Assert.Equal(new[] { "oz", "g" }, options.Select(option => option.Label));
    }

    [Fact]
    public void SharedDedupeCleansLabelsAndCollapsesTheSameGramsInTheSameUnit()
    {
        const string json = """
            [{"label":"  large   egg ","unit":"g","grams":50,"source":"FDC"},
             {"label":"large egg","unit":"g","grams":50.004,"source":"FDC"},
             {"label":"cup","unit":"cup","grams":50.004,"source":"FDC"}]
            """;

        var options = FoodServingOptions.BuildOptions(servingSize: null, servingSizeUnit: null, servingOptionsJson: json);
        var stored = options.Where(option => option.Source == "FDC").ToList();

        Assert.Equal(2, stored.Count);
        Assert.Equal("large egg", stored[0].Label);
        Assert.Equal("cup", stored[1].Label);

        Assert.Contains(options, option => option.Label == "g" && option.Unit == "g");
        Assert.Contains(options, option => option.Label == "oz" && option.Unit == "oz");
    }

    [Fact]
    public void SharedServingOptionParseTreatsMissingOrMalformedJsonAsEmpty()
    {
        Assert.Empty(FoodServingOptions.ReadOptions(null));
        Assert.Empty(FoodServingOptions.ReadOptions("  "));
        Assert.Empty(FoodServingOptions.ReadOptions("{not json"));
    }
}
