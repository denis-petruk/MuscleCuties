using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Session;

public interface IWorkoutSessionActionStore
{
    Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken);
    Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken);

    Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken);
    Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId, CancellationToken cancellationToken);
    Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken);
}
