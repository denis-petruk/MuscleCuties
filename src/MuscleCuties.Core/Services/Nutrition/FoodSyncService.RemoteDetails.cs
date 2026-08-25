using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService
{
    private const int DetailBatchSize = 8;
    private const int DetailRefreshLimit = 8;

    private const int Calories = 1008;
    private const int CaloriesAtwaterGeneral = 2047;
    private const int CaloriesAtwaterSpecific = 2048;
    private const int Protein = 1003;
    private const int Carbs = 1005;
    private const int Fats = 1004;

    private async Task<List<FoodItem>> GetRemotePageItemsAsync(string query, IEnumerable<int> fdcIds)
    {
        var ids = fdcIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var foods = await _nutritionRepository.GetFoodItemsByFdcIdsAsync(ids);
        return FoodSearchResultFilter.PrepareFoodItems(query, foods);
    }

    private async Task<List<FdcFoodDetail>> GetRemoteDetailsAsync(
        IEnumerable<int> fdcIds,
        CancellationToken cancellationToken)
    {
        var details = new List<FdcFoodDetail>();
        foreach (var batch in fdcIds.Distinct().Chunk(DetailBatchSize))
        {
            details.AddRange(await _fdcApiClient.GetFoodsAsync(batch, cancellationToken));
        }

        return details;
    }

    private static List<int> SelectDetailRefreshIds(IReadOnlyList<FdcFoodSearchResult> remoteResults) =>
        remoteResults
            .Where(NeedsDetailRefresh)
            .Select(result => result.FdcId)
            .Distinct()
            .Take(DetailRefreshLimit)
            .ToList();

    private static bool NeedsDetailRefresh(FdcFoodSearchResult result) =>
        !HasSearchCalories(result) || CountSearchPrimaryMacros(result) < 2;

    private static bool HasSearchCalories(FdcFoodSearchResult result) =>
        HasSearchNutrient(result, Calories) ||
        HasSearchNutrient(result, CaloriesAtwaterGeneral) ||
        HasSearchNutrient(result, CaloriesAtwaterSpecific);

    private static bool HasSearchNutrient(FdcFoodSearchResult result, int nutrientId) =>
        result.FoodNutrients.Any(n => n.NutrientId == nutrientId && n.Value is > 0f);

    private static int CountSearchPrimaryMacros(FdcFoodSearchResult result) =>
        Convert.ToInt32(HasSearchNutrient(result, Protein)) +
        Convert.ToInt32(HasSearchNutrient(result, Carbs)) +
        Convert.ToInt32(HasSearchNutrient(result, Fats));

    private static List<FdcFoodDetail> BuildDetailsFromSearchResults(
        IReadOnlyList<FdcFoodSearchResult> searchResults,
        IEnumerable<FdcFoodDetail> fetchedDetails)
    {
        var details = fetchedDetails.ToList();
        var detailsById = details
            .GroupBy(detail => detail.FdcId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var result in searchResults)
        {
            if (detailsById.TryGetValue(result.FdcId, out var detail))
            {
                ApplySearchMetadata(detail, result);
                continue;
            }

            var fallbackDetail = CreateDetailFromSearchResult(result);
            details.Add(fallbackDetail);
            detailsById[result.FdcId] = fallbackDetail;
        }

        return details;
    }

    private static void ApplySearchMetadata(FdcFoodDetail detail, FdcFoodSearchResult result)
    {
        detail.Description = FirstPresent(detail.Description, result.Description) ?? string.Empty;
        detail.DataType = FirstPresent(detail.DataType, result.DataType);
        detail.BrandOwner = FirstPresent(detail.BrandOwner, result.BrandOwner);
        detail.BrandName = FirstPresent(detail.BrandName, result.BrandName);
        detail.GtinUpc = FirstPresent(detail.GtinUpc, result.GtinUpc);
        detail.Ingredients = FirstPresent(detail.Ingredients, result.Ingredients);
        detail.ServingSize ??= result.ServingSize;
        detail.ServingSizeUnit = FirstPresent(detail.ServingSizeUnit, result.ServingSizeUnit);
        detail.HouseholdServingFullText = FirstPresent(detail.HouseholdServingFullText, result.HouseholdServingFullText);
        detail.PackageWeight = FirstPresent(detail.PackageWeight, result.PackageWeight);

        foreach (var nutrient in result.FoodNutrients.Where(n => n.NutrientId > 0 && n.Value.HasValue))
        {
            if (HasNutrient(detail, nutrient.NutrientId))
                continue;

            detail.FoodNutrients.Add(new FdcFoodDetailNutrient
            {
                NutrientId = nutrient.NutrientId,
                Value = nutrient.Value
            });
        }
    }

    private static FdcFoodDetail CreateDetailFromSearchResult(FdcFoodSearchResult result)
    {
        var detail = new FdcFoodDetail
        {
            FdcId = result.FdcId,
            Description = result.Description,
            DataType = result.DataType,
            BrandOwner = result.BrandOwner,
            BrandName = result.BrandName,
            GtinUpc = result.GtinUpc,
            Ingredients = result.Ingredients,
            ServingSize = result.ServingSize,
            ServingSizeUnit = result.ServingSizeUnit,
            HouseholdServingFullText = result.HouseholdServingFullText,
            PackageWeight = result.PackageWeight
        };

        foreach (var nutrient in result.FoodNutrients.Where(n => n.NutrientId > 0 && n.Value.HasValue))
        {
            detail.FoodNutrients.Add(new FdcFoodDetailNutrient
            {
                NutrientId = nutrient.NutrientId,
                Value = nutrient.Value
            });
        }

        return detail;
    }

    private static bool HasNutrient(FdcFoodDetail detail, int nutrientId) =>
        detail.FoodNutrients.Any(n =>
        {
            var detailNutrientId = n.Nutrient?.Id > 0 ? n.Nutrient.Id : n.NutrientId;
            return detailNutrientId == nutrientId;
        });

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
