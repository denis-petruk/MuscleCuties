using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface IWorkoutRepository : IRepository<WorkoutPlan>
{
    Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId);
    Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId);
    Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId);
    Task<WorkoutPlan?> GetActivePlanAsync(int userId);
}