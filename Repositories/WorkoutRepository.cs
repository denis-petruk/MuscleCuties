using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;
using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public class WorkoutRepository(AppDatabase db) : BaseRepository<WorkoutPlan>(db), IWorkoutRepository
{
    public async Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId) =>
        await _db.WorkoutPlans
            .Include(p => p.WorkoutDays)
            .ThenInclude(d => d.WorkoutExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(p => p.Id == planId);

    public async Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId) =>
        await _db.WorkoutDays
            .Where(d => d.WorkoutPlanId == planId)
            .Include(d => d.WorkoutExercises)
            .ThenInclude(we => we.Exercise)
            .OrderBy(d => d.DayNumber)
            .ToListAsync();

    public async Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId) =>
        await _db.WorkoutExercises
            .Where(we => we.WorkoutDayId == workoutDayId)
            .Include(we => we.Exercise)
            .Select(we => we.Exercise!)
            .ToListAsync();

    public async Task<WorkoutPlan?> GetActivePlanAsync(int userId) =>
        await _db.WorkoutPlans
            .Where(p => p.UserId == userId)
            .Include(p => p.WorkoutDays)
            .FirstOrDefaultAsync();
}