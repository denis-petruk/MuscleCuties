using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;

namespace MuscleCuties.Core.Services.Workout;

public interface IWorkoutService
{
    Task<WorkoutPlanSummary> GetPlanSummaryAsync(int userId, CyclePhase phase);

    Task<TodaysWorkoutSummary> GetTodaysSummaryAsync(
        int userId,
        CyclePhase phase,
        DateTime date);

    Task<WorkoutSessionDetail> GetWorkoutSessionDetailAsync(int userId, int workoutDayId);

    Task LogWorkoutSessionAsync(
        int userId,
        int workoutDayId,
        IReadOnlyCollection<WorkoutExerciseLogInput> exerciseLogs,
        DateTime date);

    Task<IReadOnlyList<ExerciseSwapOption>> GetSwapCandidatesAsync(int userId, int workoutDayExerciseId);

    Task SwapExerciseAsync(int userId, int workoutDayExerciseId, int newExerciseId, bool savePreference);

    Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleAsync(
        int userId,
        DateTime startDate,
        int weeksCount = 4,
        CyclePhase? phase = null);

    Task RescheduleWorkoutAsync(
        int userId,
        DateTime sourceDate,
        DateTime targetDate);

    Task RescheduleWorkoutDayAsync(
        int userId,
        int workoutDayId,
        DateTime targetDate);

    Task<string?> GetMovementNotesAsync(int userId, int exerciseId);

    Task SetMovementNotesAsync(int userId, int exerciseId, string? notes);

    Task<string?> GetMovementNotesByNameAsync(int userId, string exerciseName);

    Task SetMovementNotesByNameAsync(int userId, string exerciseName, string? notes);

    Task<string?> GetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId);

    Task SetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId, string? notes);
}
