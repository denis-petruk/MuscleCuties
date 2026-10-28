using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresHiitEvidenceCaptureStore(PostgresDbContext context, TimeProvider clock) : IHiitEvidenceCaptureStore
{
    public async Task<HiitEvidenceCaptureChallengeDto> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
            var state = await context.WorkoutAccountPlanningStates.SingleAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            var now = clock.GetUtcNow();
            var date = LocalDate(state.TimeZoneId, now);
            var expiry = now.Add(HiitEvidenceCaptureRule.ChallengeLifetime);
            var challenge = new HiitEvidenceCaptureChallengeRecord
            {
                Id = Guid.NewGuid(), UserId = userId, IssuedAtUtc = now.UtcDateTime,
                ExpiresAtUtc = expiry.UtcDateTime, IssuedLocalDate = date, TimeZoneVersion = state.TimeZoneVersion
            };
            context.HiitEvidenceCaptureChallenges.Add(challenge);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new HiitEvidenceCaptureChallengeDto(challenge.Id, expiry);
        }).ConfigureAwait(false);
    }

    // Caller must hold the account lock and commit this change atomically with the captured answers.
    internal async Task<HiitEvidenceCaptureStamp?> ConsumeInCurrentTransactionAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        var state = await context.WorkoutAccountPlanningStates.SingleAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        var challenge = await context.HiitEvidenceCaptureChallenges.SingleOrDefaultAsync(row => row.UserId == userId && row.Id == challengeId,
            cancellationToken).ConfigureAwait(false);
        if (challenge is null) return null;
        var now = clock.GetUtcNow();
        if (!HiitEvidenceCaptureRule.CanConsume(new DateTimeOffset(challenge.IssuedAtUtc, TimeSpan.Zero),
            new DateTimeOffset(challenge.ExpiresAtUtc, TimeSpan.Zero), now, challenge.IssuedLocalDate,
            LocalDate(state.TimeZoneId, now), challenge.TimeZoneVersion, state.TimeZoneVersion,
            challenge.ConsumedAtUtc is not null)) return null;
        challenge.ConsumedAtUtc = now.UtcDateTime;
        return new HiitEvidenceCaptureStamp(challenge.IssuedLocalDate, challenge.TimeZoneVersion);
    }

    private static DateOnly LocalDate(string? zoneId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) throw new InvalidOperationException("Establish the account timezone first.");
        try { return WorkoutAccountCalendarRule.LocalDate(now, TimeZoneInfo.FindSystemTimeZoneById(zoneId)); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("Establish the account timezone first."); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("Establish the account timezone first."); }
    }
}
