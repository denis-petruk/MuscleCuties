using System.Text.Json;
using System.Text.RegularExpressions;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public sealed class FoodServingOption
{
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public float Grams { get; set; }
    public string Source { get; set; } = string.Empty;
}

public static partial class FoodServingOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> CustomFoodUnits { get; } =
    [
        "g",
        "ml",
        "oz",
        "lb",
        "cup",
        "fl oz",
        "tbsp",
        "tsp"
    ];

    private static IReadOnlyList<FoodServingOption> StandardOptions { get; } =
    [
        new FoodServingOption { Label = "g", Unit = "g", Grams = 1f, Source = "Standard" },
        new FoodServingOption { Label = "oz", Unit = "oz", Grams = 28.3495f, Source = "Standard" },
        new FoodServingOption { Label = "lb", Unit = "lb", Grams = 453.592f, Source = "Standard" },
        new FoodServingOption { Label = "ml", Unit = "ml", Grams = 1f, Source = "Standard" },
        new FoodServingOption { Label = "cup", Unit = "cup", Grams = 240f, Source = "Standard" },
        new FoodServingOption { Label = "fl oz", Unit = "fl oz", Grams = 29.5735f, Source = "Standard" },
        new FoodServingOption { Label = "tbsp", Unit = "tbsp", Grams = 15f, Source = "Standard" },
        new FoodServingOption { Label = "tsp", Unit = "tsp", Grams = 5f, Source = "Standard" }
    ];

    public static string? CreateOptionsJson(FdcFoodDetail detail)
    {
        var options = BuildFdcOptions(detail);
        return options.Count == 0 ? null : JsonSerializer.Serialize(options, JsonOptions);
    }

    public static IReadOnlyList<FoodServingOption> BuildOptionsForFood(FoodItem food)
    {
        var options = ReadOptions(food.ServingOptionsJson).ToList();

        if (food.ServingSize is > 0f &&
            TryConvertToGrams(food.ServingSize.Value, food.ServingSizeUnit, out var servingGrams))
        {
            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = "serving",
                    Unit = "serving",
                    Grams = servingGrams,
                    Source = "Serving"
                });
        }

        foreach (var option in StandardOptions)
            AddDistinctOption(options, option);

        return options;
    }

    public static IReadOnlyList<FoodServingOption> ReadOptions(string? servingOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(servingOptionsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<FoodServingOption>>(servingOptionsJson, JsonOptions)?
                .Where(option => !string.IsNullOrWhiteSpace(option.Label) && option.Grams > 0f)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool TryConvertToGrams(float amount, string? unit, out float grams)
    {
        grams = 0f;
        if (amount <= 0f || string.IsNullOrWhiteSpace(unit))
            return false;

        var normalized = NormalizeUnit(unit);
        grams = normalized switch
        {
            "g" => amount,
            "kg" => amount * 1000f,
            "mg" => amount / 1000f,
            "oz" => amount * 28.3495f,
            "lb" => amount * 453.592f,
            "ml" => amount,
            "l" => amount * 1000f,
            "cup" => amount * 240f,
            "fl oz" => amount * 29.5735f,
            "tbsp" => amount * 15f,
            "tsp" => amount * 5f,
            _ => 0f
        };

        return grams > 0f;
    }

    private static List<FoodServingOption> BuildFdcOptions(FdcFoodDetail detail)
    {
        var options = new List<FoodServingOption>();

        if (detail.ServingSize is > 0f &&
            TryConvertToGrams(detail.ServingSize.Value, detail.ServingSizeUnit, out var servingGrams))
        {
            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = "serving",
                    Unit = "serving",
                    Grams = servingGrams,
                    Source = "FDC"
                });
        }

        if (TryParsePackageWeight(detail.PackageWeight, out var containerGrams))
        {
            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = "container",
                    Unit = "container",
                    Grams = containerGrams,
                    Source = "FDC"
                });
        }

        foreach (var portion in detail.FoodPortions)
        {
            if (portion.GramWeight is not > 0f)
                continue;

            var amount = portion.Amount is > 0f ? portion.Amount.Value : 1f;
            var grams = portion.GramWeight.Value / amount;
            var label = BuildPortionLabel(portion);

            if (string.IsNullOrWhiteSpace(label))
                continue;

            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = label,
                    Unit = label,
                    Grams = grams,
                    Source = "FDC"
                });
        }

        return options;
    }

    private static string BuildPortionLabel(FdcFoodPortion portion)
    {
        var unit = FirstPresent(
            portion.MeasureUnit?.Abbreviation,
            portion.MeasureUnit?.Name,
            portion.Modifier,
            portion.PortionDescription);

        if (string.IsNullOrWhiteSpace(unit))
            return string.Empty;

        unit = unit.Trim().ToLowerInvariant();
        return NormalizeUnit(unit) switch
        {
            "g" => "g",
            "oz" => "oz",
            "cup" => "cup",
            "fl oz" => "fl oz",
            "tbsp" => "tbsp",
            "tsp" => "tsp",
            _ => CleanLabel(unit)
        };
    }

    private static bool TryParsePackageWeight(string? packageWeight, out float grams)
    {
        grams = 0f;
        if (string.IsNullOrWhiteSpace(packageWeight))
            return false;

        foreach (Match match in PackageWeightRegex().Matches(packageWeight).Cast<Match>().Reverse())
        {
            if (!float.TryParse(match.Groups["amount"].Value, out var amount))
                continue;

            if (TryConvertToGrams(amount, match.Groups["unit"].Value, out grams))
                return true;
        }

        return false;
    }

    private static void AddDistinctOption(List<FoodServingOption> options, FoodServingOption option)
    {
        if (option.Grams <= 0f || string.IsNullOrWhiteSpace(option.Label))
            return;

        var label = CleanLabel(option.Label);
        if (options.Any(existing =>
                string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase) ||
                Math.Abs(existing.Grams - option.Grams) < 0.01f &&
                string.Equals(existing.Unit, option.Unit, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        options.Add(new FoodServingOption
        {
            Label = label,
            Unit = string.IsNullOrWhiteSpace(option.Unit) ? label : CleanLabel(option.Unit),
            Grams = option.Grams,
            Source = option.Source
        });
    }

    private static string NormalizeUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant().Replace(".", string.Empty);
        return normalized switch
        {
            "gram" or "grams" or "gm" => "g",
            "kilogram" or "kilograms" or "kgs" => "kg",
            "milligram" or "milligrams" => "mg",
            "ounce" or "ounces" => "oz",
            "pound" or "pounds" or "lbs" => "lb",
            "milliliter" or "milliliters" or "millilitre" or "millilitres" => "ml",
            "liter" or "liters" or "litre" or "litres" => "l",
            "cups" => "cup",
            "fluid ounce" or "fluid ounces" or "floz" or "fl ounce" or "fl ounces" => "fl oz",
            "tablespoon" or "tablespoons" or "tbs" => "tbsp",
            "teaspoon" or "teaspoons" => "tsp",
            _ => normalized
        };
    }

    private static string CleanLabel(string value) =>
        WhiteSpaceRegex().Replace(value.Trim(), " ");

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    [GeneratedRegex(@"(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|kilograms?|g|grams?|mg|milligrams?|lb|lbs|pounds?|oz|ounces?)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageWeightRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
