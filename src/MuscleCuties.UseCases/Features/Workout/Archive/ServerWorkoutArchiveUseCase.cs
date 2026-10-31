namespace MuscleCuties.UseCases.Features.Workout.Archive;

public sealed class ServerWorkoutArchiveUseCase(IServerWorkoutArchiveStore store)
{
    public Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        store.GetTotalSessionsCountAsync(userId, cancellationToken);

    public Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        store.GetWorkoutHistoryCountByMonthAsync(userId, year, month, cancellationToken);
}
