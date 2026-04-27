using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class CycleRepository(AppDatabase db) : BaseRepository<CycleLog>(db), ICycleRepository
{
    public async Task<CycleLog?> GetLatestCycleAsync(int userId) =>
        await _db.CycleLogs
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CycleStartDate)
            .FirstOrDefaultAsync();

    public async Task<List<CycleLog>> GetCycleHistoryAsync(int userId) =>
        await _db.CycleLogs
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CycleStartDate)
            .ToListAsync();
}
