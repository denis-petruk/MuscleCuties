using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public interface IFoodLookupService
{
    Task<IReadOnlyList<FoodItem>> SearchAsync(string query, int maxResults = 20);

    Task<FoodItem?> GetByIdAsync(int foodItemId);
}

public sealed class FoodLookupService : IFoodLookupService
{
    private readonly INutritionRepository _nutritionRepository;
    private readonly IFoodSyncService? _foodSyncService;

    public FoodLookupService(
        INutritionRepository nutritionRepository,
        IFoodSyncService? foodSyncService = null)
    {
        _nutritionRepository = nutritionRepository;
        _foodSyncService = foodSyncService;
    }

    public async Task<IReadOnlyList<FoodItem>> SearchAsync(string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        maxResults = Math.Clamp(maxResults, 1, 50);

        if (_foodSyncService is not null)
        {
            try
            {
                var apiFoods = await _foodSyncService.SearchAsync(query, maxResults);
                if (apiFoods.Count > 0)
                    return FoodSearchResultFilter.PrepareFoodItems(query, apiFoods);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
            {
                // Fall through to local search
            }
        }

        var localFoods = await _nutritionRepository.SearchFoodItemsAsync(query);
        return FoodSearchResultFilter
            .PrepareFoodItems(query, localFoods)
            .Take(maxResults)
            .ToList();
    }

    public async Task<FoodItem?> GetByIdAsync(int foodItemId)
    {
        if (foodItemId <= 0)
            return null;

        return await _nutritionRepository.GetByIdAsync(foodItemId);
    }
}
