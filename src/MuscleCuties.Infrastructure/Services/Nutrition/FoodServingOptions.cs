using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using MuscleCuties.Core.Domain.Nutrition;
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
    private const float DistinctGramTolerance = 0.01f;
    private const float OnePortion = FoodPortionMassRule.OneGram;
    private const float NoGrams = FoodPortionMassRule.NoGrams;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> CustomFoodUnits { get; } =
    [
        "g",
        "oz",
        "lb"
    ];

    private static IReadOnlyList<FoodServingOption> StandardOptions { get; } =
    [
        new() { Label = "g", Unit = "g", Grams = OnePortion, Source = "Standard" },
        new() { Label = "oz", Unit = "oz", Grams = FoodPortionMassRule.GramsPerOunce, Source = "Standard" },
        new() { Label = "lb", Unit = "lb", Grams = FoodPortionMassRule.GramsPerPound, Source = "Standard" }
    ];

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    public static string? CreateOptionsJson(FdcFoodDetail detail)
    {
        var options = BuildFdcOptions(detail);
        return options.Count == 0 ? null : JsonSerializer.Serialize(options, JsonOptions);
    }

    public static IReadOnlyList<FoodServingOption> BuildOptionsForFood(FoodItem food)
    {
        return BuildOptions(food.ServingSize, food.ServingSizeUnit, food.ServingOptionsJson);
    }

    public static IReadOnlyList<FoodServingOption> BuildOptions(float? servingSize, string? servingSizeUnit,
        string? servingOptionsJson)
    {
        var options = ReadOptions(servingOptionsJson).ToList();

        if (servingSize is > NoGrams && float.IsFinite(servingSize.Value) &&
            TryConvertToGrams(servingSize.Value, servingSizeUnit, out var servingGrams))
            AddDistinctOption(
                options,
                new FoodServingOption
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

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    public static IReadOnlyList<FoodServingOption> ReadOptions(string? servingOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(servingOptionsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<FoodServingOption>>(servingOptionsJson, JsonOptions)?
                .Where(option => !string.IsNullOrWhiteSpace(option.Label) &&
                                 float.IsFinite(option.Grams) && option.Grams > NoGrams)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool TryConvertToGrams(float amount, string? unit, out float grams)
    {
        return FoodPortionMassRule.TryConvertMassToGrams(amount, unit, out grams);
    }

    private static List<FoodServingOption> BuildFdcOptions(FdcFoodDetail detail)
    {
        var options = new List<FoodServingOption>();

        if (detail.ServingSize is > NoGrams && float.IsFinite(detail.ServingSize.Value) &&
            TryConvertToGrams(detail.ServingSize.Value, detail.ServingSizeUnit, out var servingGrams))
            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = "serving",
                    Unit = "serving",
                    Grams = servingGrams,
                    Source = "FDC"
                });

        if (TryParsePackageWeight(detail.PackageWeight, out var containerGrams))
            AddDistinctOption(
                options,
                new FoodServingOption
                {
                    Label = "container",
                    Unit = "container",
                    Grams = containerGrams,
                    Source = "FDC"
                });

        foreach (var portion in detail.FoodPortions)
        {
            if (portion.GramWeight is not > NoGrams || !float.IsFinite(portion.GramWeight.Value))
                continue;

            var amount = portion.Amount is > NoGrams && float.IsFinite(portion.Amount.Value)
                ? portion.Amount.Value : OnePortion;
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
        grams = NoGrams;
        if (string.IsNullOrWhiteSpace(packageWeight))
            return false;

        foreach (var match in PackageWeightRegex().Matches(packageWeight).Reverse())
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
        if (!float.IsFinite(option.Grams) || option.Grams <= NoGrams || string.IsNullOrWhiteSpace(option.Label))
            return;

        var label = CleanLabel(option.Label);
        if (options.Any(existing =>
                string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase) ||
                (Math.Abs(existing.Grams - option.Grams) < DistinctGramTolerance &&
                 string.Equals(existing.Unit, option.Unit, StringComparison.OrdinalIgnoreCase))))
            return;

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

    private static string CleanLabel(string value)
    {
        return WhiteSpaceRegex().Replace(value.Trim(), " ");
    }

    private static string? FirstPresent(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    [GeneratedRegex(
        @"(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|kilograms?|g|grams?|mg|milligrams?|lb|lbs|pounds?|oz|ounces?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PackageWeightRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
