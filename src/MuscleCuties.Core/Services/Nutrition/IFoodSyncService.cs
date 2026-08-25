using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public interface IFoodSyncService
{
    Task<List<FoodItem>> SearchAsync(
        string query,
        int pageSize = 15,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FdcFoodSearchResult>> SearchRemoteAsync(
        string query,
        int pageSize = 15,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default);
    Task<int> SeedFoodsAsync(IEnumerable<int> fdcIds, CancellationToken cancellationToken = default);
}
