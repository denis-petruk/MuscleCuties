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

            var detailIds = SelectDetailRefreshIds(remoteResults);
            IReadOnlyList<FdcFoodDetail> fetchedDetails = [];
            if (detailIds.Count > 0)
                try
                {
                    fetchedDetails = await GetRemoteDetailsAsync(detailIds, timeout.Token);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    errors.Add("FDC detail refresh timed out. Search-result nutrition was used instead.");
                }
                catch (HttpRequestException ex)
                {
                    errors.Add($"FDC detail refresh failed: {ex.Message}");
                }

            var details = BuildDetailsFromSearchResults(
                remoteResults,
                fetchedDetails);

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

    public async Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        if (fdcId <= 0)
            return null;

        var cached = await _nutritionRepository.GetFoodItemByFdcIdAsync(fdcId);
        if (IsUsableCachedFdcFood(cached))
            return cached;

        var log = await StartLogAsync();
        var errors = new List<string>();

        try
        {
            var detail = await DatabaseOperationGate.AwaitExternalAsync(
                () => _fdcApiClient.GetFoodAsync(fdcId, cancellationToken));
            if (detail is null)
            {
                log.ItemsFailed = 1;
                errors.Add($"FDC food {fdcId} was not found.");
                await CompleteLogAsync(log, "Failed", errors);
                return null;
            }

            var item = await UpsertFoodAsync(detail);
            log.ItemsUpserted = 1;
            await CompleteLogAsync(log, "Success", errors);
            return item;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            throw;
        }
    }

    private static bool IsUsableCachedFdcFood(FoodItem? food)
    {
        return food is { LastSyncedAt: not null, Calories: > 0f };
    }
}
