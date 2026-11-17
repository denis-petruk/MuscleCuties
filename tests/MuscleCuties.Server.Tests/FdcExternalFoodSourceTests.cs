using System.Text.Json;
using MuscleCuties.Server.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class FdcExternalFoodSourceTests
{
    [Fact]
    public void FdcExternalFoodSource_Parse_ParsesNutrientsCorrectly()
    {
        var json = """
        {
            "foods": [
                {
                    "fdcId": 173904,
                    "description": "Oats, rolled, dry",
                    "dataType": "Foundation",
                    "foodNutrients": [
                        { "nutrientId": 1008, "value": 379.0 },
                        { "nutrientId": 1003, "value": 13.15 },
                        { "nutrientId": 1004, "value": 6.52 },
                        { "nutrientId": 1005, "value": 67.70 }
                    ]
                },
                {
                    "fdcId": 999999,
                    "description": "Incomplete Food (Missing Carbs)",
                    "foodNutrients": [
                        { "nutrientId": 1008, "value": 100.0 },
                        { "nutrientId": 1003, "value": 10.0 },
                        { "nutrientId": 1004, "value": 5.0 }
                    ]
                }
            ]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var items = FdcExternalFoodSource.Parse(doc.RootElement);

        Assert.Single(items);
        var item = items[0];
        Assert.Equal(173904, item.FdcId);
        Assert.Equal("Oats, rolled, dry", item.Name);
        Assert.Equal(379f, item.CaloriesPer100Grams);
        Assert.Equal(13.15f, item.ProteinPer100Grams);
        Assert.Equal(6.52f, item.FatsPer100Grams);
        Assert.Equal(67.70f, item.CarbsPer100Grams);
        Assert.Equal("Foundation", item.DataType);
    }

    [Fact]
    public void FdcExternalFoodSource_Parse_EmptyOrInvalidPayloadReturnsEmptyList()
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        Assert.Empty(FdcExternalFoodSource.Parse(emptyDoc.RootElement));

        using var nonObjectDoc = JsonDocument.Parse("[]");
        Assert.Empty(FdcExternalFoodSource.Parse(nonObjectDoc.RootElement));
    }
}
