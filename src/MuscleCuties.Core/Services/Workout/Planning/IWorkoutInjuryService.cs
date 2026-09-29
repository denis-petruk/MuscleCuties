using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWorkoutInjuryService
{
    Task<IReadOnlyList<WorkoutInjuryLog>> GetActiveAsync(int userId);
    Task<IReadOnlyList<WorkoutInjuryLog>> GetAllAsync(int userId);
    Task SaveAsync(WorkoutInjuryLog log);
    Task DeleteAsync(int userId, int id);
    Task<(IReadOnlyList<string> Blocked, IReadOnlyList<string> Recommended)> GetExerciseImpactAsync(
        InjuryFlag activeInjuries);
}

public sealed class WorkoutInjuryService(IWorkoutInjuryRepository repository) : IWorkoutInjuryService
{
    public Task<IReadOnlyList<WorkoutInjuryLog>> GetActiveAsync(int userId) => repository.GetActiveAsync(userId);

    public Task<IReadOnlyList<WorkoutInjuryLog>> GetAllAsync(int userId) => repository.GetAllAsync(userId);

    public Task SaveAsync(WorkoutInjuryLog log) => repository.SaveAsync(log);

    public Task DeleteAsync(int userId, int id) => repository.DeleteAsync(userId, id);

    public Task<(IReadOnlyList<string> Blocked, IReadOnlyList<string> Recommended)> GetExerciseImpactAsync(
        InjuryFlag activeInjuries) => repository.GetExerciseImpactAsync(activeInjuries);
}
