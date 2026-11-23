using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>
///     Single projection from a catalog candidate to the food catalog wire DTO. Catalog search and
///     barcode lookup both return the same shape with the same provenance summary and nutrition
///     summary, so the mapping lives here rather than in each use case.
/// </summary>
public static class FoodCatalogItemMapper
{
    public static FoodCatalogItemDto Map(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var sourceSummary = FoodCatalogProvenanceRule.Classify(candidate) switch
        {
            FoodCatalogSourceKind.CustomFood => "Nutrition_Source_CustomFood",
            FoodCatalogSourceKind.UsdaFoodDataCentral => "Nutrition_Source_UsdaFoodDataCentral",
            _ => "Nutrition_Source_LocalFood"
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
            FoodCatalogIdentity.ToKey(candidate)
        );
        return item with { NutritionSummary = FoodCatalogNutritionSummary.Build(item) };
    }
}
