using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IFoodSyncRepository : IRepository<FoodSyncLog>
{
    Task AddSyncLogAsync(FoodSyncLog log);
    Task UpdateSyncLogAsync(FoodSyncLog log);
    Task<FoodSyncLog?> GetLatestSyncLogAsync();
    Task AddFoodItemVersionAsync(FoodItemVersion version);
}