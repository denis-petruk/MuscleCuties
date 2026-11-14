using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>
///     The catalogue a loopback Debug build searches. A Debug app points at `http://127.0.0.1:5080`,
///     and a local API without a migrated catalogue answers every search with an empty page, which made
///     the picker look broken: the field accepted text and no row ever appeared. This small synthetic
///     list gives the picker real rows to render and select without touching any shared environment.
///     Production and Beta keep the cloud catalogue; nothing here runs there.
/// </summary>
public static class DevelopmentFoodCatalog
{
    private const int FirstSyntheticId = 9_000_001;

    private static readonly IReadOnlyList<FoodCatalogCandidate> Foods =
    [
        Build(0, "Chicken breast, grilled", 165, 31, 0, 3.6f),
        Build(1, "Chicken thigh, roasted", 209, 26, 0, 10.9f),
        Build(2, "Egg, whole, boiled", 155, 13, 1.1f, 10.6f),
        Build(3, "Oatmeal, cooked", 71, 2.5f, 12, 1.5f),
        Build(4, "Rice, white, cooked", 130, 2.7f, 28, 0.3f),
        Build(5, "Greek yogurt, plain", 59, 10, 3.6f, 0.4f),
        Build(6, "Salmon, baked", 208, 20, 0, 13),
        Build(7, "Broccoli, steamed", 35, 2.4f, 7.2f, 0.4f)
    ];

    /// <summary>Case-insensitive partial-name match, the same shape the catalog search endpoint uses.</summary>
    public static IReadOnlyList<FoodCatalogCandidate> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var trimmed = query.Trim();
        return Foods
            .Where(food => food.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static FoodCatalogCandidate Build(int index, string name, float calories, float protein, float carbs, float fats) =>
        new(FirstSyntheticId + index, name, calories, protein, carbs, fats,
            IsCustom: false, FdcId: null, DataType: null, BrandName: null, BrandOwner: null,
            GtinUpc: null, Ingredients: null, ServingSize: 100, ServingSizeUnit: "g",
            ServingOptionsJson: null);
}
