using System.Net.Http;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService : IFoodSyncService
{
    private const int DefaultSearchPageSize = 15;
    private static readonly TimeSpan InteractiveSearchTimeout = TimeSpan.FromSeconds(5);

    private readonly INutritionRepository _nutritionRepository;
    private readonly IFoodSyncRepository _foodSyncRepository;
    private readonly IFdcApiClient _fdcApiClient;

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

        var local = await _nutritionRepository.SearchFoodItemsAsync(query);
        var preparedLocal = FoodSearchResultFilter.PrepareFoodItems(query, local);
        if (string.IsNullOrWhiteSpace(query))
            return local;

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
            {
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
            return preparedLocal;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            return preparedLocal;
        }

        await CompleteLogAsync(log, BuildStatus(log, errors), errors);
        if (remotePageItems.Count > 0)
            return remotePageItems;

        return pageNumber == 1 ? preparedLocal : [];
    }

    public Task<IReadOnlyList<FdcFoodSearchResult>> SearchRemoteAsync(
        string query,
        int pageSize = DefaultSearchPageSize,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        _fdcApiClient.SearchFoodsAsync(
            query,
            Math.Clamp(pageSize, 1, 50),
            Math.Max(1, pageNumber),
            cancellationToken);

    public async Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        var log = await StartLogAsync();
        var errors = new List<string>();

        try
        {
            var detail = await _fdcApiClient.GetFoodAsync(fdcId, cancellationToken);
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

    public async Task<int> SeedFoodsAsync(IEnumerable<int> fdcIds, CancellationToken cancellationToken = default)
    {
        var log = await StartLogAsync();
        var errors = new List<string>();

        try
        {
            var details = await _fdcApiClient.GetFoodsAsync(fdcIds, cancellationToken);
            await UpsertFoodsAsync(details, log, errors);
            await CompleteLogAsync(log, BuildStatus(log, errors), errors);
            return log.ItemsUpserted;
        }
        catch (InvalidOperationException ex)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            throw;
        }
        catch (HttpRequestException ex)
        {
            await CompleteLogAsync(log, "Failed", errors, ex);
            return log.ItemsUpserted;
        }
    }
}
