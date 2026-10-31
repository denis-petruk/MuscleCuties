namespace MuscleCuties.UseCases.Features.Workout.Archive;

public interface IServerWorkoutArchiveStore
{
    Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
