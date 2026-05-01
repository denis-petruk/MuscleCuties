using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IWorkoutRepository : IRepository<WorkoutPlan>
{
    Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId);
    Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId);
    Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId);
    Task<WorkoutPlan?> GetActivePlanAsync(int userId);
    Task AddWorkoutLogAsync(WorkoutLog log);
    Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date);
}