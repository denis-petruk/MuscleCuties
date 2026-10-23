namespace MuscleCuties.UseCases.Features.Workout.Archive;

public interface IWorkoutArchiveStore
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
