using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Search and barcode both return a food item; both paths own the same provenance, identity and
/// nutrition summary. These assertions pin the single mapping those callers now share.
/// </summary>
public sealed class FoodCatalogItemMapperTests
{
    private static FoodCatalogCandidate Candidate(
        bool isCustom = false, int? fdcId = null, Guid? userFoodItemId = null) =>
        new(7, "Rolled Oats", 389f, 16.9f, 66.3f, 6.9f, isCustom, fdcId, "Foundation",
            "Oats Co", null, null, null, 40f, "g", null, userFoodItemId);

    [Fact]
    public void Local_catalog_food_carries_the_local_source_key()
    {
        var item = FoodCatalogItemMapper.ToDto(Candidate());

        Assert.Equal(FoodCatalogProvenanceRule.LocalFoodSourceKey, item.SourceSummary);
        Assert.False(item.IsCustom);
    }

    [Fact]
    public void Barcode_style_usda_food_carries_the_usda_source_key()
    {
        var item = FoodCatalogItemMapper.ToDto(Candidate(fdcId: 1234));

        Assert.Equal(FoodCatalogProvenanceRule.UsdaFoodDataCentralSourceKey, item.SourceSummary);
    }

    [Fact]
    public void Account_food_carries_the_custom_source_key()
    {
        var item = FoodCatalogItemMapper.ToDto(Candidate(isCustom: true, userFoodItemId: Guid.NewGuid()));

        Assert.Equal(FoodCatalogProvenanceRule.CustomFoodSourceKey, item.SourceSummary);
        Assert.True(item.IsCustom);
    }

    [Fact]
    public void Identity_key_and_nutrition_summary_are_filled_for_every_source()
    {
        foreach (var candidate in new[] { Candidate(), Candidate(fdcId: 9), Candidate(isCustom: true, userFoodItemId: Guid.NewGuid()) })
        {
            var item = FoodCatalogItemMapper.ToDto(candidate);

            Assert.Equal(FoodCatalogIdentity.ToKey(candidate), item.FoodKey);
            Assert.Equal(FoodCatalogNutritionSummary.Build(item), item.NutritionSummary);
            Assert.False(string.IsNullOrWhiteSpace(item.NutritionSummary));
            Assert.Equal(candidate.Name, item.Name);
            Assert.Equal(candidate.Calories, item.Calories);
        }
    }
}
