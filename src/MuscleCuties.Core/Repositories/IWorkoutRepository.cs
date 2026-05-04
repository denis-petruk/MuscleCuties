using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Repositories;

public interface IWorkoutRepository : IRepository<WorkoutPlan>
{
    Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId);
    Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId);
    Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId);
    Task<WorkoutPlan?> GetActivePlanAsync(int userId);
    Task AddWorkoutLogAsync(WorkoutLog log);
    Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date);
    Task<int> GetWorkoutLogCountAsync(int userId);
    Task<WorkoutLog?> GetLatestActiveWorkoutLogAsync(int userId);
    Task<WorkoutPlan?> GetPlanByPhaseAsync(int userId, CyclePhase phase);
    Task<Exercise?> GetExerciseByCodeAsync(string code);
    Task<List<Exercise>> GetExercisesByCodesAsync(IEnumerable<string> codes);
    Task<WorkoutDay?> GetWorkoutDayWithExercisesAsync(int workoutDayId);
    Task<List<WorkoutPlan>> GetAllUserPlansAsync(int userId);
    Task DeactivateAllUserPlansAsync(int userId);
}
