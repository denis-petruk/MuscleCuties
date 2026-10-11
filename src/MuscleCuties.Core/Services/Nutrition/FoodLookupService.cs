using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public interface IFoodLookupService
{
    Task<IReadOnlyList<FoodItem>> SearchAsync(string query, int maxResults = 20);
    Task<IReadOnlyList<FoodItem>> SearchLocalAsync(string query, int maxResults = 20);
    Task<IReadOnlyList<FoodItem>> SearchLocalAsync(string query, int pageSize, int pageNumber);
    Task<IReadOnlyList<FoodItem>> SearchOnlineAsync(string query, int maxResults = 20, CancellationToken ct = default);

    Task<FoodItem?> GetByIdAsync(int foodItemId);
}

public sealed class FoodLookupService : IFoodLookupService
{
    private static readonly TimeSpan OnlineSearchTimeout = TimeSpan.FromSeconds(8);

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

        var local = await SearchLocalAsync(query, maxResults);
        if (local.Count > 0)
            return local;

        return await SearchOnlineAsync(query, maxResults);
    }

    public Task<IReadOnlyList<FoodItem>> SearchLocalAsync(string query, int maxResults = 20) =>
        SearchLocalAsync(query, maxResults, 1);

    public async Task<IReadOnlyList<FoodItem>> SearchLocalAsync(string query, int pageSize, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var clampedPageSize = Math.Clamp(pageSize, 1, 50);
        var safePageNumber = Math.Max(1, pageNumber);

        var localFoods = await _nutritionRepository.SearchFoodItemsAsync(query);
        return FoodSearchResultFilter.PrepareFoodItems(query, localFoods)
            .Skip((safePageNumber - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToList();
    }

    public async Task<IReadOnlyList<FoodItem>> SearchOnlineAsync(
        string query, int maxResults = 20, CancellationToken ct = default)
    {
        if (_foodSyncService is null)
            return [];

        var safeQuery = GenericFoodQueryPolicy.NormalizeOrEmpty(query);
        if (safeQuery.Length == 0)
            return [];

        var clampedLimit = Math.Clamp(maxResults, 1, 50);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(OnlineSearchTimeout);

        try
        {
            return await _foodSyncService.SearchRemoteFoodsAsync(safeQuery, clampedLimit, timeoutCts.Token);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or OperationCanceledException)
        {
            return [];
        }
    }

    public async Task<FoodItem?> GetByIdAsync(int foodItemId)
    {
        if (foodItemId <= 0)
            return null;

        return await _nutritionRepository.GetByIdAsync(foodItemId);
    }
}
