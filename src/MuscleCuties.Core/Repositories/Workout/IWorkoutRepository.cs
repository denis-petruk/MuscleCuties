using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Repositories.Workout;

public interface IWorkoutRepository : IRepository<WorkoutPlan>
{
    Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId);
    Task<WorkoutDay?> GetWorkoutDayWithExercisesAsync(int workoutDayId);
    Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId);
    Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId);
    Task<List<Exercise>> GetAllExercisesAsync();
    Task<WorkoutPlan?> GetActivePlanAsync(int userId);
    Task<WorkoutPlan> ReplaceActivePlanAsync(WorkoutPlan plan);
    Task AddWorkoutLogAsync(WorkoutLog log);
    Task ReplaceWorkoutLogAsync(WorkoutLog log);
    Task<WorkoutLog?> GetWorkoutLogForDayAsync(int userId, int workoutDayId, DateTime date);
    Task MergeWorkoutLogAsync(WorkoutLog log);
    Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date);
    Task<List<WorkoutLog>> GetWorkoutLogsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
    Task<List<WorkoutExerciseLog>> GetExerciseLogsByExerciseIdsAsync(int userId, IReadOnlyCollection<int> exerciseIds);
}
