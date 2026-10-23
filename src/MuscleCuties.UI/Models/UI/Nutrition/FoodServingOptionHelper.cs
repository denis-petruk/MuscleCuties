using System.Text.Json;
using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public static class FoodServingOptionHelper
{
    private const float NoGrams = FoodPortionMassRule.NoGrams;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<FoodServingOptionItem> StandardOptions =
    [
        new() { Label = "g", Unit = "g", Grams = FoodPortionMassRule.OneGram, Source = "Standard" },
        new() { Label = "oz", Unit = "oz", Grams = FoodPortionMassRule.GramsPerOunce, Source = "Standard" },
        new() { Label = "lb", Unit = "lb", Grams = FoodPortionMassRule.GramsPerPound, Source = "Standard" }
    ];

    public static IReadOnlyList<string> CustomFoodUnits { get; } =
    [
        "g",
        "oz",
        "lb"
    ];

    public static List<FoodServingOptionItem> BuildServingOptionItems(FoodSearchResultItem food)
    {
        var options = ReadOptions(food.ServingOptionsJson);

        if (food.ServingSize is > NoGrams && float.IsFinite(food.ServingSize.Value) &&
            TryConvertToGrams(food.ServingSize.Value, food.ServingSizeUnit, out var servingGrams))
            AddDistinctOption(options, new FoodServingOptionItem
            {
                Label = "serving",
                Unit = "serving",
                Grams = servingGrams,
                Source = "Serving"
            });

        foreach (var option in StandardOptions)
            AddDistinctOption(options, option);

        return options;
    }

    private static List<FoodServingOptionItem> ReadOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<FoodServingOptionItem>>(json, JsonOptions)?
                .Where(o => !string.IsNullOrWhiteSpace(o.Label) &&
                            float.IsFinite(o.Grams) && o.Grams > NoGrams)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void AddDistinctOption(List<FoodServingOptionItem> options, FoodServingOptionItem option)
    {
        if (!float.IsFinite(option.Grams) || option.Grams <= NoGrams ||
            string.IsNullOrWhiteSpace(option.Label))
            return;

        if (options.Any(o => string.Equals(o.Label, option.Label, StringComparison.OrdinalIgnoreCase)))
            return;

        options.Add(option);
    }

    internal static bool TryConvertToGrams(float amount, string? unit, out float grams)
    {
        return FoodPortionMassRule.TryConvertMassToGrams(amount, unit, out grams);
    }
}
