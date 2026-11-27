using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Session;

/// <summary>Session actions: skip, swap and complete, each answering the session's new state.</summary>
public interface IWorkoutSessionActionUseCase
{
    Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(int workoutDayId, CancellationToken cancellationToken = default);
    Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(int workoutDayId, CancellationToken cancellationToken = default);
    Task<ActiveWorkoutSessionSnapshot> StartPlannedSessionAsync(int workoutDayId, Guid sessionId, DateTime startedAtUtc, CancellationToken cancellationToken = default);
    Task<ActiveWorkoutSessionSnapshot> FinishPlannedSessionAsync(Guid sessionId, DateTime completedAtUtc, int totalDurationSeconds, int? rpeRating = null, decimal? distanceKm = null, CancellationToken cancellationToken = default);
    Task AbandonPlannedSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ActiveWorkoutSessionSnapshot> ResumePlannedTimingAsync(Guid sessionId, Guid operationId, CancellationToken cancellationToken = default);
    Task<ActiveWorkoutSessionSnapshot> CheckpointPlannedTimingAsync(Guid sessionId, Guid operationId, Guid intervalId,
        int measuredSeconds, bool pause, CancellationToken cancellationToken = default);
    Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken = default);
    Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default);

    Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken = default);

    Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default);

    Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken = default);
    Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request, CancellationToken cancellationToken = default);
    Task SplitSupersetAsync(SplitWorkoutSupersetRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Applies a session action, refusing one for a session that is no longer open.</summary>
public sealed class WorkoutSessionActionUseCase(IWorkoutSessionActionStore store) : IWorkoutSessionActionUseCase
{
    /// <summary>The planned session's history for one workout day.</summary>
    public Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(int workoutDayId, CancellationToken cancellationToken = default) => store.GetPlannedSessionHistoryAsync(workoutDayId, cancellationToken);
    /// <summary>The active session of a planned workout day.</summary>
    public Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(int workoutDayId, CancellationToken cancellationToken = default) => store.GetPlannedSessionAsync(workoutDayId, cancellationToken);
    /// <summary>Starts the planned session.</summary>
    public Task<ActiveWorkoutSessionSnapshot> StartPlannedSessionAsync(int workoutDayId, Guid sessionId, DateTime startedAtUtc, CancellationToken cancellationToken = default) => store.StartPlannedSessionAsync(workoutDayId, sessionId, startedAtUtc, cancellationToken);
    /// <summary>Finishes the planned session with its totals.</summary>
    public Task<ActiveWorkoutSessionSnapshot> FinishPlannedSessionAsync(Guid sessionId, DateTime completedAtUtc, int totalDurationSeconds, int? rpeRating = null, decimal? distanceKm = null, CancellationToken cancellationToken = default) => store.FinishPlannedSessionAsync(sessionId, completedAtUtc, totalDurationSeconds, rpeRating, distanceKm, cancellationToken);
    /// <summary>Abandons the planned session.</summary>
    public Task AbandonPlannedSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => store.AbandonPlannedSessionAsync(sessionId, cancellationToken);
    /// <summary>Resumes the planned session's paused timing.</summary>
    public Task<ActiveWorkoutSessionSnapshot> ResumePlannedTimingAsync(Guid sessionId, Guid operationId, CancellationToken cancellationToken = default) => store.ResumePlannedTimingAsync(sessionId, operationId, cancellationToken);
    /// <summary>Records progress of the planned session's timed interval.</summary>
    public Task<ActiveWorkoutSessionSnapshot> CheckpointPlannedTimingAsync(Guid sessionId, Guid operationId, Guid intervalId, int measuredSeconds, bool pause, CancellationToken cancellationToken = default) => store.CheckpointPlannedTimingAsync(sessionId, operationId, intervalId, measuredSeconds, pause, cancellationToken);
    /// <summary>The session in full, refusing one that is not hers.</summary>
    public Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayId));
        return store.GetDetailAsync(workoutDayId, cancellationToken);
    }

    /// <summary>Records the session as performed, refusing one that is already finished.</summary>
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
        if (request.Exercises?.SelectMany(exercise => exercise.ObservedSets ?? [])
            .Any(set => set.LeftSideReps.HasValue != set.RightSideReps.HasValue ||
                set.LeftSideReps is < 1 or > 1000 || set.RightSideReps is < 1 or > 1000) == true)
            throw new ArgumentException("Unilateral set repetitions must be supplied for both sides.", nameof(request));
        return store.LogAsync(request, cancellationToken);
    }

    /// <summary>The swap candidates for one exercise.</summary>
    public Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(
        int workoutDayExerciseId, CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayExerciseId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetSwapCandidatesAsync(workoutDayExerciseId, cancellationToken);
    }

    /// <summary>Replaces an exercise, refusing a swap that would remove the session's only work for a muscle group.</summary>
    public Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidSwap(request.WorkoutDayExerciseId, request.NewExerciseId))
            throw new ArgumentException("Invalid exercise swap input.", nameof(request));
        return store.SwapAsync(request, cancellationToken);
    }

    /// <summary>The movement note saved for an exercise.</summary>
    public Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId,
        CancellationToken cancellationToken = default)
    {
        if (!WorkoutSessionActionRule.IsValidIdentifier(workoutDayExerciseId))
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));
        return store.GetMovementNoteAsync(workoutDayExerciseId, cancellationToken);
    }

    /// <summary>Saves a movement note.</summary>
    public Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidMovementNote(request.WorkoutDayExerciseId, request.Notes))
            throw new ArgumentException("Invalid movement note.", nameof(request));
        return store.SaveMovementNoteAsync(request, cancellationToken);
    }

    /// <summary>Marks the exercise optional or required.</summary>
    public Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutSessionActionRule.IsValidOptionalExercise(request.WorkoutDayExerciseId, request.OptionalSets))
            throw new ArgumentException("Invalid optional exercise input.", nameof(request));
        return store.SetExerciseOptionalAsync(request, cancellationToken);
    }

    /// <summary>Splits the superset into separate exercises.</summary>
    public Task SplitSupersetAsync(SplitWorkoutSupersetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId == Guid.Empty || !WorkoutSessionActionRule.IsValidIdentifier(request.WorkoutDayExerciseId) ||
            request.SupersetGroupId <= 0)
            throw new ArgumentException("Invalid superset split input.", nameof(request));
        return store.SplitSupersetAsync(request, cancellationToken);
    }
}
