using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;

namespace MuscleCuties.UseCases.Features.Workout.Session;

public interface IWorkoutSessionActionStore
{
    Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(int workoutDayId, CancellationToken cancellationToken);
    Task<ActiveWorkoutSessionSnapshot> StartPlannedSessionAsync(int workoutDayId, Guid sessionId, DateTime startedAtUtc, CancellationToken cancellationToken);
    Task<ActiveWorkoutSessionSnapshot> FinishPlannedSessionAsync(Guid sessionId, DateTime completedAtUtc, int totalDurationSeconds, CancellationToken cancellationToken);
    Task AbandonPlannedSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken);
    Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken);

    Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken);
    Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId, CancellationToken cancellationToken);
    Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken);
    Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request, CancellationToken cancellationToken);
}
