using MuscleCuties.Core.Models.Workout;

namespace MuscleCuties.Core.Services.Workout;

public interface IWorkoutArchiveService
{
    Task<IReadOnlyList<WorkoutSessionSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset,
        CancellationToken ct = default);

    Task<WorkoutSessionDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default);
    Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default);
    Task<Guid> SaveWorkoutSessionAsync(WorkoutSessionDetailDto session, CancellationToken ct = default);

    /// <summary>
    ///     Saves live set detail against the existing aggregate workout log for the same plan day and date.
    ///     The derived ID lets legacy migration and live recording refer to one archive session.
    /// </summary>
    Task<Guid> SaveLinkedWorkoutSessionAsync(
        WorkoutSessionDetailDto session, int workoutDayId, DateTime workoutDate, CancellationToken ct = default);

    /// <summary>
    ///     Returns sessions filtered to a specific year and month, ordered by most recent first.
    ///     Passing month = 0 returns all sessions for that year.
    /// </summary>
    Task<IReadOnlyList<WorkoutSessionSummaryDto>> GetWorkoutHistoryByMonthAsync(
        int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default);

    /// <summary>
    ///     Updates an individual exercise set's weight and reps in-place for retroactive corrections.
    /// </summary>
    Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default);

    /// <summary>
    ///     Returns the total count of non-deleted sessions for the current user.
    /// </summary>
    Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default);

    /// <summary>Returns every month containing a session for the signed-in user, newest first.</summary>
    Task<IReadOnlyList<(int Year, int Month)>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default);

    /// <summary>Counts sessions for one month, or the whole year when month is zero.</summary>
    Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default);
}
