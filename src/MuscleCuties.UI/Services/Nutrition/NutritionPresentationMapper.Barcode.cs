using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>Conversions between contract payloads and the shapes the nutrition screens bind to.</summary>
public static partial class NutritionPresentationMapper
{
    /// <summary>The scanned catalog item as a food.</summary>
    public static FoodCatalogItem FromCatalogItem(FoodCatalogItemDto item) =>
        new(item.Id, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats, item.ServingSize,
            item.ServingSizeUnit, item.ServingOptionsJson, item.SourceSummary, item.NutritionSummary,
            item.IsCustom, item.FoodKey);
}
