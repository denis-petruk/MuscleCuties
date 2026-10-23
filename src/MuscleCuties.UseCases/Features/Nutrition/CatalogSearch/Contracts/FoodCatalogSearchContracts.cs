namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

public static class FoodCatalogSearchDefaults
{
    public const int DefaultPageSize = 15;
    public const int FirstPageNumber = 1;
}

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

public static class FoodCatalogNutritionSummary
{
    public const string UnknownNutrient = "Unknown";
    public const int RoundingDecimals = 1;

    public static string Format(float? value) => value is { } known
        ? Math.Round(known, RoundingDecimals).ToString(System.Globalization.CultureInfo.CurrentCulture)
        : UnknownNutrient;

    public static string Build(FoodCatalogItemDto item) =>
        $"{Format(item.Calories)} kcal | P: {Format(item.Protein)}g  C: {Format(item.Carbs)}g  F: {Format(item.Fats)}g";
}

public sealed record FoodCatalogSearchResponse(
    IReadOnlyList<FoodCatalogItemDto> Items,
    int PageNumber,
    int PageSize,
    bool HasMore
);
