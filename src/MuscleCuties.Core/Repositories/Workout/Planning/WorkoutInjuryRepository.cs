using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Workout.Planning;

public interface IWorkoutInjuryRepository
{
    Task<IReadOnlyList<WorkoutInjuryLog>> GetActiveAsync(int userId);
    Task<IReadOnlyList<WorkoutInjuryLog>> GetAllAsync(int userId);
    Task SaveAsync(WorkoutInjuryLog log);
    Task DeleteAsync(int id);
    Task<(IReadOnlyList<string> Blocked, IReadOnlyList<string> Recommended)> GetExerciseImpactAsync(
        InjuryFlag activeInjuries);
}

public class WorkoutInjuryRepository : BaseRepository<WorkoutInjuryLog>, IWorkoutInjuryRepository
{
    public WorkoutInjuryRepository(AppDatabase db) : base(db)
    {
    }

    public async Task<IReadOnlyList<WorkoutInjuryLog>> GetActiveAsync(int userId)
    {
        return await _db.Set<WorkoutInjuryLog>()
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.Status != "Cleared")
            .OrderByDescending(log => log.Date)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkoutInjuryLog>> GetAllAsync(int userId)
    {
        return await _db.Set<WorkoutInjuryLog>()
            .AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Date)
            .ToListAsync();
    }

    public async Task SaveAsync(WorkoutInjuryLog log)
    {
        if (log.Id == 0)
        {
            if (log.CreatedAt == default)
                log.CreatedAt = DateTime.UtcNow;

            await _db.Set<WorkoutInjuryLog>().AddAsync(log);
        }
        else
        {
            var existing = await _db.Set<WorkoutInjuryLog>().FindAsync(log.Id);
            if (existing is not null)
            {
                existing.SiteFlag = log.SiteFlag;
                existing.Status = log.Status;
                existing.Pain = log.Pain;
                existing.Since = log.Since;
                existing.Date = log.Date;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var log = await _db.Set<WorkoutInjuryLog>().FindAsync(id);
        if (log is null)
            return;

        _db.Set<WorkoutInjuryLog>().Remove(log);
        await _db.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<string> Blocked, IReadOnlyList<string> Recommended)> GetExerciseImpactAsync(
        InjuryFlag activeInjuries)
    {
        if (activeInjuries == InjuryFlag.None)
            return ([], []);

        var exercises = await _db.WorkoutExerciseDefinitions
            .AsNoTracking()
            .Select(exercise => new
            {
                exercise.Name,
                exercise.Contraindications,
                exercise.PreferredFor
            })
            .ToListAsync();

        var blocked = exercises
            .Where(exercise => (exercise.Contraindications & activeInjuries) != InjuryFlag.None)
            .Select(exercise => exercise.Name)
            .OrderBy(name => name)
            .ToList();
        var recommended = exercises
            .Where(exercise => (exercise.PreferredFor & activeInjuries) != InjuryFlag.None)
            .Select(exercise => exercise.Name)
            .OrderBy(name => name)
            .ToList();

        return (blocked, recommended);
    }
}
