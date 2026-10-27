using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresWorkoutProgressStore(PostgresDbContext context) : IWorkoutProgressStore
{
    private const string ReadyStatus = "Ready";
    private const string UninitializedStatus = "CalendarNotEstablished";
    private const string RejectedStatus = "StaleOrInvalidTimeZoneUpdate";
    private const string ProposedStatus = "Proposed";
    private const string ExpiredStatus = "Expired";
    private const int InitialExperienceVersion = 0;

    public Task<WorkoutProgressResponse> ReadAsync(Guid userId, CancellationToken cancellationToken) =>
        InTransactionAsync(userId, async () =>
        {
            return await SnapshotAsync(userId, ReadyStatus, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<WorkoutProgressResponse> AcceptForegroundTimeZoneAsync(Guid userId,
        WorkoutForegroundTimeZoneRequest request, CancellationToken cancellationToken) =>
        InTransactionAsync(userId, async () =>
        {
            var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
                cancellationToken).ConfigureAwait(false);
            if (!WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(request.IsForeground, request.ExpectedVersion,
                    state?.TimeZoneVersion ?? WorkoutAccountCalendarRule.InitialTimeZoneVersion, request.TimeZoneId))
                return await SnapshotAsync(userId, RejectedStatus, cancellationToken).ConfigureAwait(false);

            if (state is null)
            {
                var baselineText = await context.UserProfiles.Where(row => row.UserId == userId)
                    .Select(row => row.TrainingExperienceLevel).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (!Enum.TryParse<TrainingExperienceLevel>(baselineText, out var baseline) || !Enum.IsDefined(baseline) ||
                    baseline == TrainingExperienceLevel.Unknown)
                    return await SnapshotAsync(userId, UninitializedStatus, cancellationToken).ConfigureAwait(false);
                state = new WorkoutAccountPlanningStateRecord
                {
                    Id = Guid.NewGuid(), UserId = userId, BaselineExperience = baseline, EffectiveExperience = baseline
                };
                context.WorkoutAccountPlanningStates.Add(state);
            }
            state.TimeZoneId = request.TimeZoneId;
            state.TimeZoneVersion = checked(state.TimeZoneVersion + 1);
            var previews = await context.WorkoutPlanProposalsV02.Where(row => row.UserId == userId && row.Status == ProposedStatus)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var nowUtc = DateTime.UtcNow;
            foreach (var preview in previews)
            {
                preview.Status = ExpiredStatus;
                preview.DecidedAtUtc = nowUtc;
                preview.LeaseExpiresAtUtc = nowUtc;
                preview.ConcurrencyVersion++;
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await SnapshotAsync(userId, ReadyStatus, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private async Task<WorkoutProgressResponse> SnapshotAsync(Guid userId, string status, CancellationToken cancellationToken)
    {
        var state = await context.WorkoutAccountPlanningStates.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        var days = await context.WorkoutCompletionDayCredits.CountAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return new WorkoutProgressResponse(state is null && status == ReadyStatus ? UninitializedStatus : status,
            state?.TimeZoneId, state?.TimeZoneVersion ?? WorkoutAccountCalendarRule.InitialTimeZoneVersion,
            state?.EffectiveExperience.ToString(), days, state?.ExperienceVersion ?? InitialExperienceVersion, state?.PromotionPending ?? false);
    }

    private async Task<T> InTransactionAsync<T>(Guid userId, Func<Task<T>> action, CancellationToken cancellationToken) =>
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
            var result = await action().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);

    /// <summary>Called only by the verified planned-workout completion transaction, never manual archive writes.</summary>
    internal static async Task CreditPlannedCompletionAsync(PostgresDbContext context, Guid userId,
        WorkoutLogRecord log, DateTime nowUtc, CancellationToken cancellationToken) =>
        await CreditAsync(context, userId, log.Date, log.Id, null, nowUtc, cancellationToken).ConfigureAwait(false);

    internal static async Task CreditPlannedSessionAsync(PostgresDbContext context, Guid userId,
        WorkoutSessionLogRecord session, DateTime nowUtc, CancellationToken cancellationToken) =>
        await CreditAsync(context, userId, null, null, session.Id, nowUtc, cancellationToken).ConfigureAwait(false);

    private static async Task CreditAsync(PostgresDbContext context, Guid userId, DateOnly? recordedDate,
        Guid? sourceWorkoutLogId, Guid? sourceSessionId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
        var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (state?.TimeZoneId is not { } zoneId)
            throw new InvalidOperationException("Refresh the account timezone before completing this workout.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var localDate = WorkoutAccountCalendarRule.LocalDate(new DateTimeOffset(nowUtc, TimeSpan.Zero), zone);
        if ((recordedDate is not null && recordedDate != localDate) || await context.WorkoutCompletionDayCredits.AnyAsync(
                row => row.UserId == userId && row.LocalDate == localDate, cancellationToken).ConfigureAwait(false))
            return;
        context.WorkoutCompletionDayCredits.Add(new WorkoutCompletionDayCreditRecord
        {
            Id = Guid.NewGuid(), UserId = userId, LocalDate = localDate, SourceWorkoutLogId = sourceWorkoutLogId,
            SourceSessionId = sourceSessionId,
            AcceptedAtUtc = nowUtc, TimeZoneId = zoneId, TimeZoneVersion = state.TimeZoneVersion
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var days = await context.WorkoutCompletionDayCredits.CountAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var promoted = WorkoutExperiencePromotionRule.Resolve(state.BaselineExperience, days);
        if (promoted != state.EffectiveExperience)
        {
            state.EffectiveExperience = promoted;
            state.ExperienceVersion = checked(state.ExperienceVersion + 1);
            state.PromotionPending = true; // Reconciler must consume reliably, respecting preview deferral.
            var profile = await context.UserProfiles.SingleAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            profile.TrainingExperienceLevel = promoted.ToString();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        }
    }
}
