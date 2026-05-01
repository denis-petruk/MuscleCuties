using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class FoodSyncRepository(AppDatabase db) : BaseRepository<FoodSyncLog>(db), IFoodSyncRepository
{
    public async Task AddSyncLogAsync(FoodSyncLog log)
    {
        await _db.FoodSyncLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSyncLogAsync(FoodSyncLog log)
    {
        _db.FoodSyncLogs.Update(log);
        await _db.SaveChangesAsync();
    }

    public async Task<FoodSyncLog?> GetLatestSyncLogAsync() =>
        await _db.FoodSyncLogs
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefaultAsync();

    public async Task AddFoodItemVersionAsync(FoodItemVersion version)
    {
        await _db.FoodItemVersions.AddAsync(version);
        await _db.SaveChangesAsync();
    }
}