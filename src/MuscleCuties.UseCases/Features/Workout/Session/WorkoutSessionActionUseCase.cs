using MuscleCuties.Core.Domain.Workout;
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
    Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request, CancellationToken cancellationToken = default);
}

public sealed class WorkoutSessionActionUseCase(IWorkoutSessionActionStore store) : IWorkoutSessionActionUseCase
{
    public Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayId));
        return store.GetDetailAsync(workoutDayId, cancellationToken);
    }

    public Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var exerciseLogs = request.Exercises?.Select(exercise => new WorkoutSessionExerciseLog(
            exercise.WorkoutDayExerciseId,
            exercise.ExerciseId,
            exercise.CompletedSets,
            exercise.CompletedReps,
            exercise.WeightKg));
        if (!WorkoutSessionActionRule.IsValidSessionLog(request.WorkoutDayId, request.Date, exerciseLogs))
            throw new ArgumentException("Invalid workout session input.", nameof(request));
        return store.LogAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(
        int workoutDayExerciseId, CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayExerciseId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetSwapCandidatesAsync(workoutDayExerciseId, cancellationToken);
    }

    public Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidSwap(request.WorkoutDayExerciseId, request.NewExerciseId))
            throw new ArgumentException("Invalid exercise swap input.", nameof(request));
        return store.SwapAsync(request, cancellationToken);
    }

    public Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayExerciseId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetMovementNoteAsync(workoutDayExerciseId, cancellationToken);
    }

    public Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidMovementNote(request.WorkoutDayExerciseId, request.Notes))
            throw new ArgumentException("Invalid movement note.", nameof(request));
        return store.SaveMovementNoteAsync(request, cancellationToken);
    }

    public Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidOptionalExercise(request.WorkoutDayExerciseId, request.OptionalSets))
            throw new ArgumentException("Invalid optional exercise input.", nameof(request));
        return store.SetExerciseOptionalAsync(request, cancellationToken);
    }
}
