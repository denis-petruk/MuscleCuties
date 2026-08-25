using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;

namespace MuscleCuties.Core.Repositories.Cycle;

public class CycleRepository(AppDatabase db) : BaseRepository<CycleLog>(db), ICycleRepository
{
    public async Task<CycleLog?> GetLatestCycleAsync(int userId) =>
        await _db.CycleLogs
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync();

    public async Task<List<CycleLog>> GetCycleHistoryAsync(int userId) =>
        await _db.CycleLogs
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

    public async Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId) =>
        await _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LoggedAt)
            .ThenByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<CyclePhaseLog?> GetLatestPhaseLogOnOrBeforeAsync(int userId, DateTime date)
    {
        var cutoffDate = date.Date;

        return await _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.LoggedAt <= cutoffDate)
            .OrderByDescending(l => l.LoggedAt)
            .ThenByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime loggedAt)
    {
        var date = loggedAt.Date;
        var nextDate = date.AddDays(1);

        return await _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.LoggedAt >= date && l.LoggedAt < nextDate)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count) =>
        await _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LoggedAt)
            .ThenByDescending(l => l.CreatedAt)
            .Take(Math.Max(1, count))
            .ToListAsync();

    public async Task AddPhaseLogAsync(CyclePhaseLog log)
    {
        await _db.CyclePhaseLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePhaseLogAsync(CyclePhaseLog log)
    {
        DetachTrackedLocal(log);
        _db.CyclePhaseLogs.Update(log);
        await _db.SaveChangesAsync();
    }
}
