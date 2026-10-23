using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Cycle;

public class CycleRepository(AppDatabase db) : BaseRepository<CycleLog>(db), ICycleRepository
{
    public async Task<CycleLog?> GetLatestCycleAsync(int userId)
    {
        return await _db.CycleLogs
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CycleLog>> GetCycleHistoryAsync(int userId)
    {
        return await _db.CycleLogs
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId)
    {
        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId);
        return await OrderCyclePhaseLogs(query, true).FirstOrDefaultAsync();
    }

    public async Task<CyclePhaseLog?> GetLatestPhaseLogOnOrBeforeAsync(int userId, DateTime date)
    {
        var (isLastCalendarDay, nextDate) = GetDateUpperBound(date);

        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId &&
                        (isLastCalendarDay ? l.LoggedAt <= nextDate : l.LoggedAt < nextDate));

        return await OrderCyclePhaseLogs(query, true).FirstOrDefaultAsync();
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

    public async Task<CyclePhaseLog?> GetLatestPhaseLogBeforeDateAsync(int userId, DateTime date)
    {
        var cutoff = date.Date;
        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.LoggedAt < cutoff);
        return await OrderCyclePhaseLogs(query, true).FirstOrDefaultAsync();
    }

    public async Task<CyclePhaseLog?> GetNextPhaseLogAfterDateAsync(int userId, DateTime date)
    {
        var cutoff = date.Date.AddDays(1);
        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.LoggedAt >= cutoff);
        return await OrderCyclePhaseLogs(query).FirstOrDefaultAsync();
    }

    public async Task<List<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count)
    {
        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId);
        return await OrderCyclePhaseLogs(query, true)
            .Take(Math.Max(1, count))
            .ToListAsync();
    }

    public async Task<List<CyclePhaseLog>> GetPhaseLogsThroughDateAsync(int userId, DateTime date)
    {
        var (isLastCalendarDay, nextDate) = GetDateUpperBound(date);

        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId &&
                          (isLastCalendarDay ? log.LoggedAt <= nextDate : log.LoggedAt < nextDate));

        return await OrderCyclePhaseLogs(query).ToListAsync();
    }

    public async Task<List<CyclePhaseLog>> GetPhaseLogsByDateRangeAsync(int userId, DateTime startDate,
        DateTime endDate)
    {
        var start = startDate.Date;
        var (isLastCalendarDay, nextDate) = GetDateUpperBound(endDate);

        var query = _db.CyclePhaseLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.LoggedAt >= start &&
                          (isLastCalendarDay ? log.LoggedAt <= nextDate : log.LoggedAt < nextDate));

        return await OrderCyclePhaseLogs(query).ToListAsync();
    }

    public async Task<List<SymptomLog>> GetSymptomsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var (isLastCalendarDay, nextDate) = GetDateUpperBound(endDate);

        return await _db.SymptomLogs
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= start &&
                        (isLastCalendarDay ? s.Date <= nextDate : s.Date < nextDate))
            .OrderBy(s => s.Date)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync();
    }

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

    public async Task<List<SymptomLog>> GetSymptomsForDateAsync(int userId, DateTime date)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        return await _db.SymptomLogs
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= targetDate && s.Date < nextDate)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task AddSymptomLogsAsync(IEnumerable<SymptomLog> symptomLogs)
    {
        await _db.SymptomLogs.AddRangeAsync(symptomLogs);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSymptomsForDateAsync(int userId, DateTime date)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        var existing = await _db.SymptomLogs
            .Where(s => s.UserId == userId && s.Date >= targetDate && s.Date < nextDate)
            .ToListAsync();

        if (existing.Count > 0)
        {
            _db.SymptomLogs.RemoveRange(existing);
            await _db.SaveChangesAsync();
        }
    }

    private static (bool IsLastCalendarDay, DateTime NextDate) GetDateUpperBound(DateTime date)
    {
        var isLastCalendarDay = date.Date == DateTime.MaxValue.Date;
        var nextDate = isLastCalendarDay ? DateTime.MaxValue : date.Date.AddDays(1);
        return (isLastCalendarDay, nextDate);
    }

    private static IOrderedQueryable<CyclePhaseLog> OrderCyclePhaseLogs(IQueryable<CyclePhaseLog> query,
        bool descending = false)
    {
        return descending
            ? query.OrderByDescending(l => l.LoggedAt).ThenByDescending(l => l.CreatedAt)
            : query.OrderBy(l => l.LoggedAt).ThenBy(l => l.CreatedAt);
    }
}
