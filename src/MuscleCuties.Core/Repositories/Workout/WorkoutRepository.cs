using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Repositories.Workout;

public class WorkoutRepository(AppDatabase db) : BaseRepository<WorkoutPlan>(db), IWorkoutRepository
{
    public async Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId) =>
        await _db.WorkoutPlans
            .AsNoTracking()
            .Include(p => p.WorkoutDays)
            .ThenInclude(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(p => p.Id == planId);

    public async Task<WorkoutDay?> GetWorkoutDayWithExercisesAsync(int workoutDayId) =>
        await _db.WorkoutDays
            .AsNoTracking()
            .Include(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(d => d.Id == workoutDayId);

    public async Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId) =>
        await _db.WorkoutDays
            .AsNoTracking()
            .Where(d => d.WorkoutPlanId == planId)
            .Include(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .OrderBy(d => d.DayOfWeek)
            .ToListAsync();

    public async Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId) =>
        await _db.WorkoutDayExercises
            .AsNoTracking()
            .Where(we => we.WorkoutDayId == workoutDayId)
            .Include(we => we.Exercise)
            .Select(we => we.Exercise!)
            .ToListAsync();

    public async Task<List<Exercise>> GetAllExercisesAsync() =>
        await _db.Exercises
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync();

    public async Task<WorkoutPlan?> GetActivePlanAsync(int userId) =>
        await _db.WorkoutPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Include(p => p.WorkoutDays)
            .FirstOrDefaultAsync();

    public async Task<WorkoutPlan> ReplaceActivePlanAsync(WorkoutPlan plan)
    {
        var activePlans = await _db.WorkoutPlans
            .Where(p => p.UserId == plan.UserId && p.IsActive)
            .ToListAsync();

        foreach (var activePlan in activePlans)
            activePlan.IsActive = false;

        plan.IsActive = true;
        await _db.WorkoutPlans.AddAsync(plan);
        await _db.SaveChangesAsync();

        return plan;
    }

    public async Task AddWorkoutLogAsync(WorkoutLog log)
    {
        await _db.WorkoutLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Date >= dayStart && l.Date < dayEnd)
            .ToListAsync();
    }

    public async Task<List<WorkoutExerciseLog>> GetExerciseLogsByExerciseIdsAsync(
        int userId,
        IReadOnlyCollection<int> exerciseIds)
    {
        if (exerciseIds.Count == 0)
            return [];

        return await _db.WorkoutExerciseLogs
            .AsNoTracking()
            .Include(l => l.WorkoutLog)
            .Where(l => l.WorkoutLog != null &&
                        l.WorkoutLog.UserId == userId &&
                        exerciseIds.Contains(l.ExerciseId))
            .OrderByDescending(l => l.WorkoutLog!.Date)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();
    }
}
