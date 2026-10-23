using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Workout.Planning;

public interface IReadinessRepository
{
    Task<DailyReadinessLog?> GetForAsync(int userId, DateOnly date);
    Task<IReadOnlyList<DailyReadinessLog>> GetRangeAsync(int userId, DateOnly start, DateOnly end);
    Task<DailyInputs?> GetInputsForAsync(int userId, DateOnly date);
    Task SaveInputsAsync(DailyReadinessLog log);
    Task<int> GetConsecutiveLowDaysAsync(int userId, DateOnly before);
}

public class ReadinessRepository : BaseRepository<DailyReadinessLog>, IReadinessRepository
{
    public ReadinessRepository(AppDatabase db) : base(db)
    {
    }

    public async Task<DailyInputs?> GetInputsForAsync(int userId, DateOnly date)
    {
        var log = await GetForAsync(userId, date);

        if (log is null)
            return null;

        var sleepQuality = log.SleepQuality is >= 1 and <= 5
            ? (SleepQuality)log.SleepQuality.Value
            : (SleepQuality?)null;
        var soreness = log.Soreness is >= 0 and <= 3 ? (SorenessLevel)log.Soreness.Value : (SorenessLevel?)null;
        var previousDayLoad = log.PreviousDayLoad.HasValue
            ? new WorkoutLoad(WorkoutLoadKind.AggregateOnly, log.PreviousDayLoad.Value)
            : null;

        return DailyInputs.CreateSafe(
            log.Date,
            log.SleepHours,
            log.Sleep3dAvg,
            log.StepsYesterday,
            log.Steps7dAvg,
            log.Energy,
            log.Pain,
            log.Bloating,
            log.WeightKg,
            sleepQuality: sleepQuality,
            soreness: soreness,
            previousDayLoad: previousDayLoad);
    }

    public async Task<DailyReadinessLog?> GetForAsync(int userId, DateOnly date)
    {
        return await _db.Set<DailyReadinessLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Date == date);
    }

    public async Task<IReadOnlyList<DailyReadinessLog>> GetRangeAsync(int userId, DateOnly start, DateOnly end)
    {
        return await _db.Set<DailyReadinessLog>()
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Date >= start && item.Date <= end)
            .OrderBy(item => item.Date)
            .ToListAsync();
    }

    public async Task SaveInputsAsync(DailyReadinessLog log)
    {
        var existing = await _db.Set<DailyReadinessLog>()
            .FirstOrDefaultAsync(item => item.UserId == log.UserId && item.Date == log.Date);

        if (existing is null)
        {
            if (log.CreatedAt == default)
                log.CreatedAt = DateTime.UtcNow;

            await _db.Set<DailyReadinessLog>().AddAsync(log);
        }
        else
        {
            existing.SleepHours = log.SleepHours;
            existing.Sleep3dAvg = log.Sleep3dAvg;
            existing.StepsYesterday = log.StepsYesterday;
            existing.Steps7dAvg = log.Steps7dAvg;
            existing.Energy = log.Energy;
            existing.Pain = log.Pain;
            existing.Bloating = log.Bloating;
            existing.WeightKg = log.WeightKg;
            existing.SleepQuality = log.SleepQuality;
            existing.Soreness = log.Soreness;
            existing.PreviousDayLoad = log.PreviousDayLoad;
            existing.ReadinessScore = log.ReadinessScore;
            existing.ReadinessTier = log.ReadinessTier;
            existing.Phase = log.Phase;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<int> GetConsecutiveLowDaysAsync(int userId, DateOnly before)
    {
        var tiers = await _db.Set<DailyReadinessLog>()
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.Date < before)
            .OrderByDescending(log => log.Date)
            .Select(log => log.ReadinessTier)
            .Take(30)
            .ToListAsync();

        return tiers.TakeWhile(tier => tier == (int)ReadinessTier.Low).Count();
    }
}
