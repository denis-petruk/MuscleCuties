using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using StoredServingOption = MuscleCuties.Core.Services.Nutrition.FoodServingOption;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public static class FoodServingOptionHelper
{
    public static List<FoodServingOptionItem> BuildServingOptionItems(
        FoodSearchResultItem food,
        FoodPortionCatalog catalog,
        IRecipePortionUseCase portionUseCase)
    {
        var options = ReadOptions(food.ServingOptionsJson, catalog.GramUnit);

        if (food.ServingSize is { } servingSize && float.IsFinite(servingSize) && servingSize > 0f &&
            portionUseCase.ConvertMassToGrams(servingSize, food.ServingSizeUnit) is
                { Succeeded: true, Grams: { } servingGrams })
            AddDistinctOption(options, new FoodServingOptionItem
            {
                Label = "serving",
                Unit = "serving",
                Grams = servingGrams,
                Source = "Serving"
            });

        foreach (var option in catalog.StandardOptions)
            AddDistinctOption(options, new FoodServingOptionItem
            {
                Label = option.Label,
                Unit = option.Unit,
                Grams = option.Grams,
                Source = "Standard"
            });

        return options;
    }

    private static List<FoodServingOptionItem> ReadOptions(string? json, string gramUnit)
    {
        // Stored serving options are read by FoodServingOptions; this helper keeps only its own display rule on
        // top - a portion picker shows rows measured in the catalog's gram unit and nothing else.
        IReadOnlyList<StoredServingOption> stored;
        try
        {
            stored = FoodServingOptions.ReadOptions(json);
        }
        catch
        {
            return [];
        }

        return stored
            .Where(option => string.Equals(option.Unit, gramUnit, StringComparison.OrdinalIgnoreCase))
            .Select(option => new FoodServingOptionItem
            {
                Label = option.Label,
                Unit = option.Unit,
                Grams = option.Grams,
                Source = option.Source
            })
            .ToList();
    }

    private static void AddDistinctOption(List<FoodServingOptionItem> options, FoodServingOptionItem option)
    {
        if (!float.IsFinite(option.Grams) || option.Grams <= 0f ||
            string.IsNullOrWhiteSpace(option.Label))
            return;

        if (options.Any(o => string.Equals(o.Label, option.Label, StringComparison.OrdinalIgnoreCase)))
            return;

        options.Add(option);
    }

}
