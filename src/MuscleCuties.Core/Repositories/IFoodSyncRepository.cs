using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

[Obsolete("Not injected anywhere. Remove AddScoped from MauiProgram.cs line 55 first, then delete this interface")]
public interface IFoodSyncRepository : IRepository<FoodSyncLog>
{
    Task AddSyncLogAsync(FoodSyncLog log);
    Task UpdateSyncLogAsync(FoodSyncLog log);
    Task<FoodSyncLog?> GetLatestSyncLogAsync();
    Task AddFoodItemVersionAsync(FoodItemVersion version);
}