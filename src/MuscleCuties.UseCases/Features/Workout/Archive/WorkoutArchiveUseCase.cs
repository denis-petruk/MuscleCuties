namespace MuscleCuties.UseCases.Features.Workout.Archive;

public interface IWorkoutArchiveUseCase
{
    Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset,
        CancellationToken ct = default);

    Task<WorkoutArchiveDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default);
    Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default);

    Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default);

    Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default);
    Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default);
    Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default);
}

public sealed class WorkoutArchiveUseCase : IWorkoutArchiveUseCase
{
    private readonly IWorkoutArchiveStore _store;

    public WorkoutArchiveUseCase(IWorkoutArchiveStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset,
        CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);
        return _store.GetWorkoutHistoryAsync(safeLimit, safeOffset, ct);
    }

    public Task<WorkoutArchiveDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default)
    {
        return _store.GetWorkoutSessionDetailAsync(sessionId, ct);
    }

    public Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default)
    {
        return _store.UpdateWorkoutSessionNotesAsync(sessionId, notes?.Trim() ?? string.Empty, rpe, ct);
    }

    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);
        return _store.GetWorkoutHistoryByMonthAsync(year, month, safeLimit, safeOffset, ct);
    }

    public Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default)
    {
        var safeWeight = Math.Max(0.0, weightKg);
        var safeReps = Math.Max(0, repsCompleted);
        return _store.UpdateExerciseSetAsync(setId, safeWeight, safeReps, ct);
    }

    public Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default)
    {
        return _store.GetTotalSessionsCountAsync(ct);
    }

    public Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default)
    {
        return _store.GetAvailableHistoryMonthsAsync(ct);
    }

    public Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        return _store.GetWorkoutHistoryCountByMonthAsync(year, month, ct);
    }
}
