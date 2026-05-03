using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Services;

public interface IWorkoutService
{
    Task GenerateUserPlansAsync(int userId, UserGoal goal, int daysPerWeek);
    Task<WorkoutDay?> GetTodaysWorkoutAsync(int userId);
    Task SyncActivePlanToPhaseAsync(int userId, CyclePhase currentPhase);
}
