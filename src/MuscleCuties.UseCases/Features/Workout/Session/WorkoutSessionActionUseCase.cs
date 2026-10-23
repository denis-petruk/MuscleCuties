using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Session;

public interface IWorkoutSessionActionUseCase
{
    Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken = default);
    Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default);

    Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken = default);

    Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default);

    Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken = default);
}

public sealed class WorkoutSessionActionUseCase(IWorkoutSessionActionStore store) : IWorkoutSessionActionUseCase
{
    public Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken = default)
    {
        if (workoutDayId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workoutDayId));
        return store.GetDetailAsync(workoutDayId, cancellationToken);
    }

    public Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkoutDayId <= 0 || request.Date.Year is < 1970 or > 2099 ||
            request.Exercises is null || request.Exercises.Any(e =>
                e.WorkoutDayExerciseId <= 0 || e.ExerciseId <= 0 || e.CompletedSets < 0 || e.CompletedReps < 0 ||
                (e.WeightKg is { } weight && (!float.IsFinite(weight) || weight < 0))))
            throw new ArgumentException("Invalid workout session input.", nameof(request));
        return store.LogAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(
        int workoutDayExerciseId, CancellationToken cancellationToken = default)
    {
        if (workoutDayExerciseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetSwapCandidatesAsync(workoutDayExerciseId, cancellationToken);
    }

    public Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkoutDayExerciseId <= 0 || request.NewExerciseId <= 0)
            throw new ArgumentException("Invalid exercise swap input.", nameof(request));
        return store.SwapAsync(request, cancellationToken);
    }

    public Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default)
    {
        if (workoutDayExerciseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetMovementNoteAsync(workoutDayExerciseId, cancellationToken);
    }

    public Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkoutDayExerciseId <= 0 || request.Notes?.Trim().Length > 2000)
            throw new ArgumentException("Invalid movement note.", nameof(request));
        return store.SaveMovementNoteAsync(request, cancellationToken);
    }
}
