using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;
using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

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