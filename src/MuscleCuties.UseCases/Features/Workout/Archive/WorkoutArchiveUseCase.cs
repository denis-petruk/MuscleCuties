namespace MuscleCuties.UseCases.Features.Workout.Archive;

/// <summary>The archive as the screens use it.</summary>
public interface IWorkoutArchiveUseCase : IWorkoutArchiveStore
{
}

/// <summary>Reads the archive and edits finished sessions, refusing an edit to another account's session.</summary>
public sealed class WorkoutArchiveUseCase : IWorkoutArchiveUseCase
{
    private readonly IWorkoutArchiveStore _store;

    /// <summary>Takes the archive store.</summary>
    public WorkoutArchiveUseCase(IWorkoutArchiveStore store)
    {
        _store = store;
    }

    /// <summary>The workout history page.</summary>
    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset,
        CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);
        return _store.GetWorkoutHistoryAsync(safeLimit, safeOffset, ct);
    }

    /// <summary>A finished session in full, refusing one that is not hers.</summary>
    public Task<WorkoutArchiveDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default)
    {
        return _store.GetWorkoutSessionDetailAsync(sessionId, ct);
    }

    /// <summary>Stores the session's notes, keeping the session otherwise untouched.</summary>
    public Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default)
    {
        return _store.UpdateWorkoutSessionNotesAsync(sessionId, notes?.Trim() ?? string.Empty, rpe, ct);
    }

    /// <summary>The workout history for a month.</summary>
    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);
        return _store.GetWorkoutHistoryByMonthAsync(year, month, safeLimit, safeOffset, ct);
    }

    /// <summary>Corrects one set, refusing a value the session could not have recorded.</summary>
    public Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default)
    {
        var safeWeight = Math.Max(0.0, weightKg);
        var safeReps = Math.Max(0, repsCompleted);
        return _store.UpdateExerciseSetAsync(setId, safeWeight, safeReps, ct);
    }

    /// <summary>How many sessions she has finished, which the profile shows.</summary>
    public Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default)
    {
        return _store.GetTotalSessionsCountAsync(ct);
    }

    /// <summary>The months that hold sessions, so the filter never offers an empty month.</summary>
    public Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default)
    {
        return _store.GetAvailableHistoryMonthsAsync(ct);
    }

    /// <summary>How many sessions that month holds, shown before the month is loaded.</summary>
    public Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        return _store.GetWorkoutHistoryCountByMonthAsync(year, month, ct);
    }
}
