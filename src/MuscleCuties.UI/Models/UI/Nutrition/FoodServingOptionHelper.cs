using System.Text.Json;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public static class FoodServingOptionHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<FoodServingOptionItem> BuildServingOptionItems(
        FoodSearchResultItem food,
        FoodPortionCatalogOutputDto catalog,
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
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<FoodServingOptionItem>>(json, JsonOptions)?
                .Where(o => !string.IsNullOrWhiteSpace(o.Label) &&
                            float.IsFinite(o.Grams) && o.Grams > 0f &&
                            string.Equals(o.Unit, gramUnit, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
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
