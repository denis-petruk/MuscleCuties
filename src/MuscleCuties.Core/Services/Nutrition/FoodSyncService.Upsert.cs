using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService
{
    private async Task UpsertFoodsAsync(
        IEnumerable<FdcFoodDetail> details,
        FoodSyncLog log,
        List<string> errors)
    {
        var detailList = details
            .Where(detail => detail.FdcId > 0)
            .GroupBy(detail => detail.FdcId)
            .Select(group => group.First())
            .ToList();

        if (detailList.Count == 0)
            return;

        var existingByFdcId = (await _nutritionRepository.GetFoodItemsByFdcIdsAsync(detailList.Select(d => d.FdcId)))
            .Where(food => food.FdcId.HasValue)
            .ToDictionary(food => food.FdcId!.Value);

        var newItems = new List<FoodItem>();
        var updatedItems = new List<FoodItem>();
        var versions = new List<FoodItemVersion>();

        foreach (var detail in detailList)
        {
            try
            {
                existingByFdcId.TryGetValue(detail.FdcId, out var existing);
                var item = BuildFoodItem(detail, existing, versions);

                if (existing is null)
                    newItems.Add(item);
                else
                    updatedItems.Add(item);

                log.ItemsUpserted++;
            }
            catch (Exception ex)
            {
                log.ItemsFailed++;
                errors.Add($"FDC food {detail.FdcId}: {ex.Message}");
            }
        }

        await _foodSyncRepository.AddFoodItemVersionsAsync(versions);
        await _nutritionRepository.SaveFoodItemsAsync(newItems, updatedItems);
    }

    private async Task<FoodItem> UpsertFoodAsync(FdcFoodDetail detail)
    {
        var versions = new List<FoodItemVersion>();
        var existing = await _nutritionRepository.GetFoodItemByFdcIdAsync(detail.FdcId);
        var item = BuildFoodItem(detail, existing, versions);

        await _foodSyncRepository.AddFoodItemVersionsAsync(versions);

        if (existing is null)
            await _nutritionRepository.AddAsync(item);
        else
            await _nutritionRepository.UpdateAsync(item);

        return item;
    }

    private static FoodItem BuildFoodItem(
        FdcFoodDetail detail,
        FoodItem? existing,
        ICollection<FoodItemVersion> versions)
    {
        var now = DateTime.UtcNow;

        if (existing is not null && FdcFoodMapper.HasNutrientChanges(existing, detail))
        {
            versions.Add(new FoodItemVersion
            {
                FoodItemId = existing.Id,
                NutrientJson = FdcFoodMapper.CreateNutrientSnapshot(existing),
                VersionedAt = now,
                ChangeSource = "FDC"
            });
        }

        return FdcFoodMapper.ApplyToFoodItem(detail, existing, now);
    }
}
