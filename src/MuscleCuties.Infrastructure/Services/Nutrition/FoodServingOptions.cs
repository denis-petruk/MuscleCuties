using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>One way to measure a food, with its weight in grams so any choice becomes a weight.</summary>
public sealed class FoodServingOption
{
    /// <summary>What the user sees, such as a household measure.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>The unit the amount is counted in before it is converted to grams.</summary>
    public string Unit { get; set; } = string.Empty;
    /// <summary>What one unit weighs, which is the conversion the diary applies.</summary>
    public float Grams { get; set; }
    /// <summary>Where the option came from, so a guessed portion can be told from a stated one.</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>Builds and reads the serving options stored with a food, with the fallbacks that keep a food usable when the source states no portion.</summary>
public static partial class FoodServingOptions
{
    private const float DistinctGramTolerance = 0.01f;
    private const float OnePortion = FoodPortionMassRule.OneGram;
    private const float NoGrams = FoodPortionMassRule.NoGrams;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Units a custom food may be saved with.</summary>
    public static IReadOnlyList<string> CustomFoodUnits { get; } =
    [
        "g",
        "oz",
        "lb"
    ];

    /// <summary>The serving options every food starts with.</summary>
    private static IReadOnlyList<FoodServingOption> StandardOptions { get; } =
    [
        new() { Label = "g", Unit = "g", Grams = OnePortion, Source = "Standard" },
        new() { Label = "oz", Unit = "oz", Grams = FoodPortionMassRule.GramsPerOunce, Source = "Standard" },
        new() { Label = "lb", Unit = "lb", Grams = FoodPortionMassRule.GramsPerPound, Source = "Standard" }
    ];

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    /// <summary>The stored JSON for a food's options, or nothing when there is nothing worth storing.</summary>
    public static string? CreateOptionsJson(FdcFoodDetail detail)
    {
        var options = BuildFdcOptions(detail);
        return options.Count == 0 ? null : JsonSerializer.Serialize(options, JsonOptions);
    }

    /// <summary>The options for a food, always including the per-100 g one.</summary>
    public static IReadOnlyList<FoodServingOption> BuildOptionsForFood(FoodItem food)
    {
        return BuildOptions(food.ServingSize, food.ServingSizeUnit, food.ServingOptionsJson);
    }

    /// <summary>Builds the serving options a food offers from its stored serving.</summary>
    public static IReadOnlyList<FoodServingOption> BuildOptions(float? servingSize, string? servingSizeUnit,
        string? servingOptionsJson)
    {
        var options = new List<FoodServingOption>();

        foreach (var stored in ReadOptions(servingOptionsJson))
            AddDistinctOption(options, stored);

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
    /// <summary>The options read back from their stored JSON, with the per-100 g option added if it is missing.</summary>
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

    /// <summary>The grams for an amount of a chosen unit, or false when the unit is unknown.</summary>
    public static bool TryConvertToGrams(float amount, string? unit, out float grams)
    {
        return FoodPortionMassRule.TryConvertMassToGrams(amount, unit, out grams);
    }

    /// <summary>The options the source's portions imply, with duplicates dropped.</summary>
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

    /// <summary>The label for a portion, preferring the household wording over the bare amount.</summary>
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

    /// <summary>A package weight read out of free text, which is how a stated size becomes an option.</summary>
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

    /// <summary>Adds an option unless the same unit and weight is already there, since sources repeat portions.</summary>
    public static void AddDistinctOption(List<FoodServingOption> options, FoodServingOption option)
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

    /// <summary>A unit in the one spelling the options compare by.</summary>
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

    /// <summary>A label with the source's whitespace and punctuation tidied.</summary>
    private static string CleanLabel(string value)
    {
        return WhiteSpaceRegex().Replace(value.Trim(), " ");
    }

    /// <summary>The first non-empty candidate, so a label falls back through the source's alternative wordings.</summary>
    private static string? FirstPresent(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

        /// <summary>The generated regex that recognizes a package weight.</summary>
    [GeneratedRegex(
        @"(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|kilograms?|g|grams?|mg|milligrams?|lb|lbs|pounds?|oz|ounces?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PackageWeightRegex();

    /// <summary>The generated regex that collapses runs of whitespace.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
