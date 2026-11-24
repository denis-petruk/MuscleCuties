using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

public static partial class NutritionPresentationMapper
{
    public static FoodCatalogItem FromCatalogItem(FoodCatalogItemDto item) =>
        new(item.Id, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats, item.ServingSize,
            item.ServingSizeUnit, item.ServingOptionsJson, item.SourceSummary, item.NutritionSummary,
            item.IsCustom, item.FoodKey);
}
