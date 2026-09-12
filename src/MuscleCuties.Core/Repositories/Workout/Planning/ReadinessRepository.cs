using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Workout.Planning;

public interface IReadinessRepository
{
    Task<DailyReadinessLog?> GetForAsync(int userId, DateOnly date);
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

        return log is null
            ? null
            : new DailyInputs(
                log.Date,
                log.SleepHours,
                log.Sleep3dAvg,
                log.StepsYesterday,
                log.Steps7dAvg,
                log.Energy,
                log.Pain,
                log.Bloating,
                log.WeightKg);
    }

    public async Task<DailyReadinessLog?> GetForAsync(int userId, DateOnly date)
    {
        return await _db.Set<DailyReadinessLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Date == date);
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
