using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public interface IFdcApiClient
{
    Task<IReadOnlyList<FdcFoodSearchResult>> SearchFoodsAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<FdcFoodDetail?> GetFoodAsync(int fdcId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FdcFoodDetail>> GetFoodsAsync(
        IEnumerable<int> fdcIds,
        CancellationToken cancellationToken = default);
}
