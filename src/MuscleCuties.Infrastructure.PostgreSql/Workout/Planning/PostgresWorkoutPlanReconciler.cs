using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Trusted job handler; a separately configured durable scheduler must invoke it.</summary>
public sealed class PostgresWorkoutPlanReconciler(PostgresDbContext dbContext) : IWorkoutPlanReconciler
{
    private const string ProposedStatus = "Proposed";
    private const string FeasibleStatus = "Feasible";
    private const string AcceptedStatus = "Accepted";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkoutReconciliationOutcome> ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        var outcome = WorkoutReconciliationOutcome.NeedsReview;
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var state = await dbContext.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
                cancellationToken).ConfigureAwait(false);
            if (state is null || (!state.PromotionPending && !state.SafetyRegenerationPending))
            {
                outcome = WorkoutReconciliationOutcome.Completed;
                return;
            }
            if (state.TimeZoneId is null)
                return;
            var nowUtc = DateTime.UtcNow;
            if (await dbContext.WorkoutPlanProposalsV02.AnyAsync(row => row.UserId == userId && row.Status == ProposedStatus &&
                    row.LeaseId != null && row.LeaseExpiresAtUtc > nowUtc, cancellationToken).ConfigureAwait(false))
            {
                outcome = WorkoutReconciliationOutcome.Deferred;
                return;
            }
            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking().Where(row => row.UserId == userId && row.IsActive)
                .Select(row => (Guid?)row.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (activePlanId is null) return; // Never create a first plan as a side effect.

            // Injury restoration needs original/replacement provenance and clearance-window handling.
            // Keep its work pending until that information can be applied without guessing.
            if (await dbContext.WorkoutDays.AsNoTracking().AnyAsync(day => day.UserId == userId &&
                    day.WorkoutPlanId == activePlanId && day.IsRemovedBySafety && day.SafetyInjuryId != null,
                    cancellationToken).ConfigureAwait(false))
                return;

            var revision = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                .Where(row => row.UserId == userId && row.PlanId == activePlanId)
                .OrderByDescending(row => row.RevisionNumber).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (revision is null) return;
            var proposal = await dbContext.WorkoutPlanProposalsV02.AsNoTracking().SingleOrDefaultAsync(
                row => row.UserId == userId && row.Id == revision.SourceProposalId, cancellationToken).ConfigureAwait(false);
            var priorInput = Deserialize<ProposalInputSnapshot>(revision.InputSnapshotJson);
            var priorPreview = proposal is null ? null : Deserialize<StrengthPlanPreviewResponseV020>(proposal.ProposalJson);
            if (priorInput?.Request is null || priorPreview?.EffectiveActivityTypeId is not { } styleId ||
                !Enum.IsDefined(typeof(WorkoutActivityType), styleId) ||
                !StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)styleId))
                return;

            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId); }
            catch (TimeZoneNotFoundException) { return; }
            catch (InvalidTimeZoneException) { return; }
            var source = await new PostgresStrengthPlanPreviewSourceV020(dbContext)
                .LoadWithinTransactionAsync(userId, cancellationToken).ConfigureAwait(false);
            if (source?.SelectedActivityTypes is null || source.AvailableWeekdays is null ||
                source.ActivitySessionDurationsMinutes is null || !source.SelectedActivityTypes.Contains(styleId))
                return;
            var applied = new WorkoutPreferencesDto(source.SelectedActivityTypes.Order().ToArray(),
                source.AvailableWeekdays.Select(day => (int)day).Order().ToArray(),
                source.ActivitySessionDurationsMinutes, source.Equipment.ToString(), source.RequestedSessionsPerWeek,
                source.ExcludedExerciseIds);
            var style = WorkoutActivityContractMapper.ToDto((WorkoutActivityType)styleId);
            var request = priorInput.Request with
            {
                Activity = style,
                ExplicitStrengthStyleChoice = style,
                LocalDate = WorkoutAccountCalendarRule.LocalDate(new DateTimeOffset(nowUtc, TimeSpan.Zero), zone),
                DraftPreferences = applied
            };
            var preview = PreviewStrengthPlanV020UseCase.EvaluateFromSnapshot(request, source);
            if (preview.Status != FeasibleStatus || preview.MixedActivitySchedule?.Sessions is not { Count: > 0 } ||
                (preview.EffectiveActivityTypeId is { } chosen && chosen != styleId))
                return;
            if (state.SafetyRegenerationPending)
            {
                var suppressedActivityIds = await dbContext.WorkoutDays.AsNoTracking()
                    .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlanId &&
                        day.IsRemovedBySafety && day.SafetyActivityTypeId != null)
                    .Select(day => day.SafetyActivityTypeId!.Value).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
                var scheduledActivityIds = preview.MixedActivitySchedule.Sessions.Select(session => session.ActivityTypeId).ToHashSet();
                if (suppressedActivityIds.Any(activity => source.SelectedActivityTypes.Contains(activity) &&
                    !scheduledActivityIds.Contains(activity)))
                    return;
            }
            var created = await new PostgresStrengthPlanProposalStoreV020(dbContext)
                .SaveWithinTransactionAsync(userId, request, preview, cancellationToken).ConfigureAwait(false);
            if (created.Lease is not { } lease) return;
            var accept = new AcceptStrengthPlanProposalRequestV020(created.ProposalId, lease.LeaseId, lease.Fence,
                created.ProposalVersion);
            var result = await new PostgresAcceptStrengthPlanProposalStoreV020(dbContext)
                .AcceptWithinTransactionAsync(userId, accept, transaction, true, cancellationToken).ConfigureAwait(false);
            if (result.Status != AcceptedStatus) return;
            state.PromotionPending = false;
            state.SafetyRegenerationPending = false;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            outcome = WorkoutReconciliationOutcome.Completed;
        }).ConfigureAwait(false);
        return outcome;
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private sealed record ProposalInputSnapshot(StrengthPlanPreviewRequestV020 Request);
}
