using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;

public sealed class PostgresWorkoutArchiveStore(PostgresDbContext dbContext) : IServerWorkoutArchiveStore
{
    private const int MinValidYear = 1;
    private const int MaxValidYear = 9998;
    private const int MinValidMonth = 0;
    private const int MaxValidMonth = 12;

    public async Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var count = await dbContext.WorkoutSessionLogs
                .CountAsync(w => w.UserId == userId && !w.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }).ConfigureAwait(false);

    public async Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var (start, end) = GetHistoryDateRange(year, month);
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var count = await dbContext.WorkoutSessionLogs
                .CountAsync(w => w.UserId == userId && !w.IsDeleted && w.StartedAt >= start && w.StartedAt < end, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }).ConfigureAwait(false);

    private static (DateTime Start, DateTime End) GetHistoryDateRange(int year, int month)
    {
        if (year is < MinValidYear or > MaxValidYear || month is < MinValidMonth or > MaxValidMonth)
            throw new ArgumentOutOfRangeException(nameof(year), "A valid year and month are required.");
        var start = new DateTime(year, month == 0 ? 1 : month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month == 0 ? start.AddYears(1) : start.AddMonths(1);
        return (start, end);
    }
}
