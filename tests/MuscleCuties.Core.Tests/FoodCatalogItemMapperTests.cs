using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
///     Pins the projection that FoodCatalogSearchUseCase and LookupBarcodeUseCase each carried
///     privately: the same DTO fields, the same provenance summary key and the same nutrition summary
///     for the same candidate, so search results and barcode results cannot drift apart.
/// </summary>
public sealed class FoodCatalogItemMapperTests
{
    [Fact]
    public void CustomFoodKeepsItsOwnProvenanceSummaryAndIdentity()
    {
        var candidate = Candidate(isCustom: true);

        var dto = FoodCatalogItemMapper.Map(candidate);

        Assert.Equal("Nutrition_Source_CustomFood", dto.SourceSummary);
        Assert.Equal(FoodCatalogIdentity.ToKey(candidate), dto.FoodKey);
        Assert.True(dto.IsCustom);
        Assert.NotNull(dto.NutritionSummary);
    }

    [Fact]
    public void FoodWithoutExternalProvenanceIsReportedAsLocal()
    {
        var candidate = Candidate(isCustom: false);

        var dto = FoodCatalogItemMapper.Map(candidate);

        Assert.Equal("Nutrition_Source_LocalFood", dto.SourceSummary);
        Assert.False(dto.IsCustom);
    }

    [Fact]
    public void ProjectionCarriesEveryNutritionFieldOfTheCandidate()
    {
        var candidate = Candidate(isCustom: false);

        var dto = FoodCatalogItemMapper.Map(candidate);

        Assert.Equal(candidate.Id, dto.Id);
        Assert.Equal(candidate.Name, dto.Name);
        Assert.Equal(candidate.Calories, dto.Calories);
        Assert.Equal(candidate.Protein, dto.Protein);
        Assert.Equal(candidate.Carbs, dto.Carbs);
        Assert.Equal(candidate.Fats, dto.Fats);
        Assert.Equal(candidate.ServingSize, dto.ServingSize);
        Assert.Equal(candidate.ServingSizeUnit, dto.ServingSizeUnit);
        Assert.Equal(candidate.ServingOptionsJson, dto.ServingOptionsJson);
    }

    private static FoodCatalogCandidate Candidate(bool isCustom) => new(
        17,
        "Greek Yogurt",
        59f,
        10f,
        3.6f,
        0.4f,
        isCustom,
        FdcId: null,
        DataType: null,
        BrandName: null,
        BrandOwner: null,
        GtinUpc: null,
        Ingredients: null,
        ServingSize: 170f,
        ServingSizeUnit: "g",
        ServingOptionsJson: null);
}
