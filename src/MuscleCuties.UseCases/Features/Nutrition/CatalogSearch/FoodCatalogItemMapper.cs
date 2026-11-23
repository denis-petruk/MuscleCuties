using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>
/// One mapping from a catalog candidate to the wire item. Search results and barcode lookups return the
/// same food shape, so the provenance key, identity key and nutrition summary are decided here once
/// instead of in every use case that happens to return a food.
/// </summary>
public static class FoodCatalogItemMapper
{
    public static FoodCatalogItemDto ToDto(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var sourceSummary = FoodCatalogProvenanceRule.Classify(candidate) switch
        {
            FoodCatalogSourceKind.CustomFood => FoodCatalogProvenanceRule.CustomFoodSourceKey,
            FoodCatalogSourceKind.UsdaFoodDataCentral => FoodCatalogProvenanceRule.UsdaFoodDataCentralSourceKey,
            _ => FoodCatalogProvenanceRule.LocalFoodSourceKey
        };

        var item = new FoodCatalogItemDto(
            candidate.Id,
            candidate.Name,
            candidate.Calories,
            candidate.Protein,
            candidate.Carbs,
            candidate.Fats,
            candidate.ServingSize,
            candidate.ServingSizeUnit,
            candidate.ServingOptionsJson,
            sourceSummary,
            string.Empty,
            candidate.IsCustom,
            FoodCatalogIdentity.ToKey(candidate));
        return item with { NutritionSummary = FoodCatalogNutritionSummary.Build(item) };
    }
}
