using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Nutrition;

public interface IFoodSyncRepository : IRepository<FoodSyncLog>
{
    Task AddSyncLogAsync(FoodSyncLog log);
    Task UpdateSyncLogAsync(FoodSyncLog log);
    Task<FoodSyncLog?> GetLatestSyncLogAsync();
    Task AddFoodItemVersionAsync(FoodItemVersion version);
    Task AddFoodItemVersionsAsync(IReadOnlyCollection<FoodItemVersion> versions);
}
