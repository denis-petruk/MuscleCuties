using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService
{
    private async Task UpsertFoodsAsync(
        IEnumerable<FdcFoodDetail> details)
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
        foreach (var detail in detailList)
            try
            {
                existingByFdcId.TryGetValue(detail.FdcId, out var existing);
                var item = BuildFoodItem(detail, existing);

                if (existing is null)
                    newItems.Add(item);
                else
                    updatedItems.Add(item);

            }
            catch (Exception)
            {
            }

        await _nutritionRepository.SaveFoodItemsAsync(newItems, updatedItems);
    }

    private async Task<FoodItem> UpsertFoodAsync(FdcFoodDetail detail)
    {
        var existing = await _nutritionRepository.GetFoodItemByFdcIdAsync(detail.FdcId);
        var item = BuildFoodItem(detail, existing);

        if (existing is null)
            await _nutritionRepository.AddAsync(item);
        else
            await _nutritionRepository.UpdateAsync(item);

        return item;
    }

    private static FoodItem BuildFoodItem(
        FdcFoodDetail detail,
        FoodItem? existing)
    {
        var now = DateTime.UtcNow;
        return FdcFoodMapper.ApplyToFoodItem(detail, existing, now);
    }
}
