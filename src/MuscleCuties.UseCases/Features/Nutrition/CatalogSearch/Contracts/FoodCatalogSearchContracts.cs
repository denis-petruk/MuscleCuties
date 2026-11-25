namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

/// <summary>The search defaults, so an empty query still returns something usable.</summary>
public static class FoodCatalogSearchDefaults
{
    public const int DefaultPageSize = 15;
    public const int FirstPageNumber = 1;
}

/// <summary>A catalog search query with its paging.</summary>
public sealed record FoodCatalogSearchRequest(
    string Query,
    int PageSize = FoodCatalogSearchDefaults.DefaultPageSize,
    int PageNumber = FoodCatalogSearchDefaults.FirstPageNumber
);

/// <param name="Id">Legacy integer projection; use <paramref name="FoodKey"/> for all references.</param>
/// <param name="FoodKey">Stable source-qualified identity for catalog and tenant custom food rows.</param>
public sealed record FoodCatalogItemDto(
    int Id,
    string Name,
    float? Calories,
    float? Protein,
    float? Carbs,
    float? Fats,
    float? ServingSize,
    string? ServingSizeUnit,
    string? ServingOptionsJson,
    string SourceSummary,
    string NutritionSummary,
    bool IsCustom,
    string FoodKey = ""
);

/// <summary>A food's per-100 g headline nutrients, shown in the result row.</summary>
public static class FoodCatalogNutritionSummary
{
    public const string UnknownNutrient = "Unknown";
    public const int RoundingDecimals = 1;

    /// <summary>Renders a nutrient value or a dash, so an unknown value never shows as zero.</summary>
    public static string Format(float? value) => value is { } known
        ? Math.Round(known, RoundingDecimals).ToString(System.Globalization.CultureInfo.CurrentCulture)
        : UnknownNutrient;

    /// <summary>The search text stored for a catalog item, built from its name and brand.</summary>
    public static string Build(FoodCatalogItemDto item) =>
        $"{Format(item.Calories)} kcal | P: {Format(item.Protein)}g  C: {Format(item.Carbs)}g  F: {Format(item.Fats)}g";
}

/// <summary>The matching foods with the total the paging needs.</summary>
public sealed record FoodCatalogSearchResponse(
    IReadOnlyList<FoodCatalogItemDto> Items,
    int PageNumber,
    int PageSize,
    bool HasMore
);
