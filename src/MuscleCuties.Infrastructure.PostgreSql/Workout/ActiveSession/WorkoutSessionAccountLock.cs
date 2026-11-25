
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

/// <summary>Serialises one account's session writes, so a start and a completion cannot interleave.</summary>
internal static class WorkoutSessionAccountLock
{
    private const string LockPrefix = "workout-session-account:";

    /// <summary>Takes the account's advisory lock so one session write runs at a time.</summary>
    public static Task<int> AcquireAsync(PostgresDbContext context, Guid userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({LockPrefix + userId.ToString("D")}, 0))",
            cancellationToken);

    /// <summary>True when the account has a preview that has not expired.</summary>
    public static Task<bool> HasActivePreviewAsync(PostgresDbContext context, Guid userId, DateTime now,
        CancellationToken cancellationToken) => context.WorkoutPlanProposalsV02.AsNoTracking().AnyAsync(
        item => item.UserId == userId && item.Status == "Proposed" && item.LeaseId != null &&
                item.LeaseExpiresAtUtc > now, cancellationToken);

    /// <summary>Refuses a session write while a plan preview is unsettled, because the preview may change what is prescribed.</summary>
    public static async Task RequireNoPreviewAsync(PostgresDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        await AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
        if (await HasActivePreviewAsync(context, userId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false))
            throw new WorkoutPreviewAlreadyOpenException();
    }
}
