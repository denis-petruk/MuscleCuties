using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService : IFoodSyncService
{
    private const int DefaultSearchPageSize = 15;
    private static readonly TimeSpan InteractiveSearchTimeout = TimeSpan.FromSeconds(5);
    private readonly IFdcApiClient _fdcApiClient;
    private readonly IFoodSyncRepository _foodSyncRepository;

    private readonly INutritionRepository _nutritionRepository;

    public FoodSyncService(
        INutritionRepository nutritionRepository,
        IFoodSyncRepository foodSyncRepository,
        IFdcApiClient fdcApiClient)
    {
        _nutritionRepository = nutritionRepository;
        _foodSyncRepository = foodSyncRepository;
        _fdcApiClient = fdcApiClient;
    }

    public async Task<List<FoodItem>> SearchAsync(
        string query,
        int pageSize = DefaultSearchPageSize,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        pageNumber = Math.Max(1, pageNumber);

        if (string.IsNullOrWhiteSpace(query))
            return [];

        var local = await _nutritionRepository.SearchFoodItemsAsync(query);
        var preparedLocal = FoodSearchResultFilter.PrepareFoodItems(query, local);
        var localPage = preparedLocal
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (localPage.Count == pageSize)
            return localPage;

        var log = await StartLogAsync();
        var errors = new List<string>();
        var remotePageItems = new List<FoodItem>();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(InteractiveSearchTimeout);

            var remoteResults = FoodSearchResultFilter.PrepareRemoteResults(
                query,
                await SearchRemoteAsync(query, pageSize, pageNumber, timeout.Token));

            // The remote boundary permits only generic food-name searches. FDC IDs
            // are not sent back to the service, even when search nutrients are sparse.
            var details = BuildDetailsFromSearchResults(remoteResults, []);

            await UpsertFoodsAsync(details, log, errors);

            remotePageItems = await GetRemotePageItemsAsync(query, details.Select(d => d.FdcId));
        }
        catch (InvalidOperationException ex)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            throw;
        }
        catch (HttpRequestException ex)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            return localPage;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            return localPage;
        }

        await CompleteLogAsync(log, BuildStatus(log, errors), errors);
        if (remotePageItems.Count > 0)
            return remotePageItems;

        return localPage;
    }

    public Task<IReadOnlyList<FdcFoodSearchResult>> SearchRemoteAsync(
        string query,
        int pageSize = DefaultSearchPageSize,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return DatabaseOperationGate.AwaitExternalAsync(() =>
            _fdcApiClient.SearchFoodsAsync(
                query,
                Math.Clamp(pageSize, 1, 50),
                Math.Max(1, pageNumber),
                cancellationToken));
    }

    public async Task<IReadOnlyList<FoodItem>> SearchRemoteFoodsAsync(
        string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var safeQuery = GenericFoodQueryPolicy.NormalizeOrEmpty(query);
        if (safeQuery.Length == 0)
            return [];

        var clampedLimit = Math.Clamp(maxResults, 1, 50);
        var results = await SearchRemoteAsync(safeQuery, clampedLimit, 1, cancellationToken);
        return FoodSearchResultFilter.PrepareFoodItems(safeQuery,
                FoodSearchResultFilter.PrepareRemoteResults(safeQuery, results)
                    .Select(result => FdcFoodMapper.ApplyToFoodItem(CreateDetailFromSearchResult(result))))
            .Take(clampedLimit)
            .ToList();
    }

    public async Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        if (fdcId <= 0)
            return null;

        var cached = await _nutritionRepository.GetFoodItemByFdcIdAsync(fdcId);
        return IsUsableCachedFdcFood(cached) ? cached : null;
    }

    private static bool IsUsableCachedFdcFood(FoodItem? food)
    {
        return food is { LastSyncedAt: not null, Calories: > 0f };
    }
}
