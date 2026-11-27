using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using StoredServingOption = MuscleCuties.Core.Services.Nutrition.FoodServingOption;

namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>Builds the portion picker's options: the food's stored options, its own serving, and the standard</summary>
/// portions, with the shared dedupe rule applied across all three.</summary>
public static class FoodServingOptionHelper
{
    /// <summary>The serving options a food can be logged in.</summary>
    public static List<FoodServingOptionItem> BuildServingOptionItems(
        FoodSearchResultItem food,
        FoodPortionCatalog catalog,
        IRecipePortionUseCase portionUseCase)
    {
        // FoodServingOptions owns the one dedupe rule: cleaned labels, and rows that repeat the same grams in the same
        // unit collapse into one. This picker adds only its own display rule - it lists rows measured in the catalog's
        // gram unit and nothing else.
        var options = new List<StoredServingOption>();

        foreach (var stored in FoodServingOptions.ReadOptions(food.ServingOptionsJson)
                     .Where(option => string.Equals(option.Unit, catalog.GramUnit, StringComparison.OrdinalIgnoreCase)))
            FoodServingOptions.AddDistinctOption(options, stored);

        if (food.ServingSize is { } servingSize && float.IsFinite(servingSize) && servingSize > 0f &&
            portionUseCase.ConvertMassToGrams(servingSize, food.ServingSizeUnit) is
            { Succeeded: true, Grams: { } servingGrams })
            FoodServingOptions.AddDistinctOption(options, new StoredServingOption
            {
                Label = "serving",
                Unit = "serving",
                Grams = servingGrams,
                Source = "Serving"
            });

        foreach (var option in catalog.StandardOptions)
            FoodServingOptions.AddDistinctOption(options, new StoredServingOption
            {
                Label = option.Label,
                Unit = option.Unit,
                Grams = option.Grams,
                Source = "Standard"
            });

        return options
            .Select(option => new FoodServingOptionItem
            {
                Label = option.Label,
                Unit = option.Unit,
                Grams = option.Grams,
                Source = option.Source
            })
            .ToList();
    }
}
