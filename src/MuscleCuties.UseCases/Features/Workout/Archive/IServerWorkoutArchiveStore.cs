namespace MuscleCuties.UseCases.Features.Workout.Archive;

public interface IServerWorkoutArchiveStore
{
    Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<WorkoutArchiveDetailDto?> GetWorkoutSessionDetailAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateWorkoutSessionNotesAsync(
        Guid userId,
        Guid sessionId,
        string notes,
        int? rpe,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        Guid userId,
        int year,
        int month,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateExerciseSetAsync(
        Guid userId,
        Guid setId,
        double weightKg,
        int repsCompleted,
        CancellationToken cancellationToken = default);

    Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExercisePersonalRecordDto>> GetPersonalRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
