using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService : IFoodSyncService
{
    private const int DefaultSearchPageSize = 15;
    private static readonly TimeSpan InteractiveSearchTimeout = TimeSpan.FromSeconds(5);
    private readonly IFdcApiClient _fdcApiClient;
    private readonly INutritionRepository _nutritionRepository;

    public FoodSyncService(
        INutritionRepository nutritionRepository,
        IFdcApiClient fdcApiClient)
    {
        _nutritionRepository = nutritionRepository;
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
                    fetchedDetails = [];
                }
                catch (HttpRequestException)
                {
                    fetchedDetails = [];
                }

            var details = BuildDetailsFromSearchResults(
                remoteResults,
                fetchedDetails);

            await UpsertFoodsAsync(details);

            remotePageItems = await GetRemotePageItemsAsync(query, details.Select(d => d.FdcId));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return preparedLocal;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return preparedLocal;
        }

        if (remotePageItems.Count > 0)
            return remotePageItems;

        return pageNumber == 1 ? preparedLocal : [];
    }

    public Task<IReadOnlyList<FdcFoodSearchResult>> SearchRemoteAsync(
        string query,
        int pageSize = DefaultSearchPageSize,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return _fdcApiClient.SearchFoodsAsync(
            query,
            Math.Clamp(pageSize, 1, 50),
            Math.Max(1, pageNumber),
            cancellationToken);
    }

    public async Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        try
        {
            var detail = await _fdcApiClient.GetFoodAsync(fdcId, cancellationToken);
            if (detail is null)
            {
                return null;
            }

            var item = await UpsertFoodAsync(detail);
            return item;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            throw;
        }
    }

}
