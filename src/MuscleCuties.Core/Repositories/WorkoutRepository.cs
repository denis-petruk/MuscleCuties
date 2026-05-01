using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class WorkoutRepository(AppDatabase db) : BaseRepository<WorkoutPlan>(db), IWorkoutRepository
{
    public async Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId) =>
        await _db.WorkoutPlans
            .Include(p => p.WorkoutDays)
            .ThenInclude(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(p => p.Id == planId);

    public async Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId) =>
        await _db.WorkoutDays
            .Where(d => d.WorkoutPlanId == planId)
            .Include(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .OrderBy(d => d.DayOfWeek)
            .ToListAsync();

    public async Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId) =>
        await _db.WorkoutDayExercises
            .Where(we => we.WorkoutDayId == workoutDayId)
            .Include(we => we.Exercise)
            .Select(we => we.Exercise!)
            .ToListAsync();

    public async Task<WorkoutPlan?> GetActivePlanAsync(int userId) =>
        await _db.WorkoutPlans
            .Where(p => p.UserId == userId && p.IsActive)
            .Include(p => p.WorkoutDays)
            .FirstOrDefaultAsync();

    public async Task AddWorkoutLogAsync(WorkoutLog log)
    {
        await _db.WorkoutLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date) =>
        await _db.WorkoutLogs
            .Where(l => l.UserId == userId && l.Date.Date == date.Date)
            .ToListAsync();
}