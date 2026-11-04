using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

internal static class WorkoutSessionAccountLock
{
    private const string LockPrefix = "workout-session-account:";

    public static Task<int> AcquireAsync(PostgresDbContext context, Guid userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({LockPrefix + userId.ToString("D")}, 0))",
            cancellationToken);

    public static Task<bool> HasActivePreviewAsync(PostgresDbContext context, Guid userId, DateTime now,
        CancellationToken cancellationToken) => context.WorkoutPlanProposalsV02.AsNoTracking().AnyAsync(
        item => item.UserId == userId && item.Status == "Proposed" && item.LeaseId != null &&
                item.LeaseExpiresAtUtc > now, cancellationToken);

    public static async Task RequireNoPreviewAsync(PostgresDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        await AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
        if (await HasActivePreviewAsync(context, userId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false))
            throw new WorkoutPreviewAlreadyOpenException();
    }
}
