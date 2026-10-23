namespace MuscleCuties.UseCases.Features.Workout.Archive;

public sealed class ServerWorkoutArchiveUseCase(IServerWorkoutArchiveStore store)
{
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;
    private const int MinRpeRating = 1;
    private const int MaxRpeRating = 10;
    private const double MinWeightKg = 0.0;
    private const int MinReps = 0;

    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, MinLimit, MaxLimit);
        var safeOffset = Math.Max(0, offset);
        return store.GetWorkoutHistoryAsync(userId, safeLimit, safeOffset, cancellationToken);
    }

    public Task<WorkoutArchiveDetailDto?> GetWorkoutSessionDetailAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        store.GetWorkoutSessionDetailAsync(userId, sessionId, cancellationToken);

    public Task<bool> UpdateWorkoutSessionNotesAsync(
        Guid userId,
        Guid sessionId,
        string? notes,
        int? rpe,
        CancellationToken cancellationToken = default)
    {
        if (rpe is < MinRpeRating or > MaxRpeRating)
            throw new ArgumentOutOfRangeException(nameof(rpe), $"RPE must be between {MinRpeRating} and {MaxRpeRating}.");

        return store.UpdateWorkoutSessionNotesAsync(
            userId,
            sessionId,
            notes?.Trim() ?? string.Empty,
            rpe,
            cancellationToken);
    }

    public Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        Guid userId,
        int year,
        int month,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, MinLimit, MaxLimit);
        var safeOffset = Math.Max(0, offset);
        return store.GetWorkoutHistoryByMonthAsync(userId, year, month, safeLimit, safeOffset, cancellationToken);
    }

    public Task<bool> UpdateExerciseSetAsync(
        Guid userId,
        Guid setId,
        double weightKg,
        int repsCompleted,
        CancellationToken cancellationToken = default)
    {
        var safeWeight = Math.Max(MinWeightKg, weightKg);
        var safeReps = Math.Max(MinReps, repsCompleted);
        return store.UpdateExerciseSetAsync(userId, setId, safeWeight, safeReps, cancellationToken);
    }

    public Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        store.GetTotalSessionsCountAsync(userId, cancellationToken);

    public Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        store.GetAvailableHistoryMonthsAsync(userId, cancellationToken);

    public Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        store.GetWorkoutHistoryCountByMonthAsync(userId, year, month, cancellationToken);

    public Task<IReadOnlyList<ExercisePersonalRecordDto>> GetPersonalRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        store.GetPersonalRecordsAsync(userId, cancellationToken);
}
