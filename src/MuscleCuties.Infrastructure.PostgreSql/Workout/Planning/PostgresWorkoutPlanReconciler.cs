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

/// <summary>Applies eligible promotion revisions under a tenant transaction and account lock.</summary>
public sealed class PostgresWorkoutPlanReconciler(PostgresDbContext context) : IWorkoutPlanReconciler
{
    private const int RequiredCurrentStrengthStyleCount = 1;
    private const string FeasibleStatus = "Feasible";
    private const string AcceptedStatus = "Accepted";
    private const string ExpiredStatus = "Expired";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkoutReconciliationOutcome> ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
            var outcome = await ReconcileInCurrentTransactionAsync(userId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return outcome;
        }).ConfigureAwait(false);
    }

    // Caller owns the tenant transaction and account lock.
    internal async Task<WorkoutReconciliationOutcome> ReconcileInCurrentTransactionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (state is null || (!state.PromotionPending && !state.SafetyRegenerationPending))
            return WorkoutReconciliationOutcome.Completed;
        if (state.TimeZoneId is null) return WorkoutReconciliationOutcome.NeedsReview;
        var now = DateTimeOffset.UtcNow;
        if (await WorkoutSessionAccountLock.HasActivePreviewAsync(context, userId, now.UtcDateTime, cancellationToken).ConfigureAwait(false) ||
            await context.WorkoutSessionLogs.AnyAsync(row => row.UserId == userId && !row.IsDeleted && row.CompletedAt == null,
                cancellationToken).ConfigureAwait(false))
            return WorkoutReconciliationOutcome.Deferred;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return WorkoutReconciliationOutcome.NeedsReview; }
        catch (InvalidTimeZoneException) { return WorkoutReconciliationOutcome.NeedsReview; }
        var today = WorkoutAccountCalendarRule.LocalDate(now, zone);
        var injuries = await context.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        // Injury revisions require restoration provenance; do not consume the shared pending flag here.
        if (state.SafetyRegenerationPending &&
            (injuries.Count > 0 || await context.WorkoutInjuryEpisodes.AsNoTracking()
                .AnyAsync(episode => episode.UserId == userId, cancellationToken).ConfigureAwait(false)))
            return WorkoutReconciliationOutcome.NeedsReview;
        if (injuries.Any(injury => !string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase) ||
            injury.ClearedDate is null || WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(injury.ClearedDate.Value, today)))
            return WorkoutReconciliationOutcome.NeedsReview;
        var activePlanId = await context.WorkoutPlans.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive).OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is null) return WorkoutReconciliationOutcome.NeedsReview;
        var styleIds = await context.WorkoutDays.AsNoTracking()
            .Where(row => row.UserId == userId && row.WorkoutPlanId == activePlanId && row.ActivityTypeId != null)
            .Select(row => row.ActivityTypeId!.Value).Distinct().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var styles = styleIds.Where(id => Enum.IsDefined((WorkoutActivityType)id) &&
            StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)id)).ToArray();
        if (styles.Length != RequiredCurrentStrengthStyleCount) return WorkoutReconciliationOutcome.NeedsReview;
        var style = (WorkoutActivityType)styles[0];
        var profile = await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null || !Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var equipment) || !Enum.IsDefined(equipment)) return WorkoutReconciliationOutcome.NeedsReview;
        var revision = await context.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(row => row.UserId == userId && row.PlanId == activePlanId)
            .OrderByDescending(row => row.RevisionNumber).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (revision is null) return WorkoutReconciliationOutcome.NeedsReview;
        AppliedInput? input;
        try { input = JsonSerializer.Deserialize<AppliedInput>(revision.InputSnapshotJson, JsonOptions); }
        catch (JsonException) { return WorkoutReconciliationOutcome.NeedsReview; }
        var previous = input?.Request;
        var draft = previous?.DraftPreferences;
        if (draft is null || previous!.WarmupMinutes is null || previous.CooldownMinutes is null ||
            !WorkoutPreferencesRule.IsValid(draft.ActivityTypes, draft.AvailableWeekdays,
                draft.ActivitySessionDurationsMinutes, draft.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(draft.EquipmentLevel)) return WorkoutReconciliationOutcome.NeedsReview;

        // Catalog serialization is needed only after account state proves this read can publish a plan.
        await PlanningCatalogFingerprint.LockCatalogAsync(context, cancellationToken).ConfigureAwait(false);
        var source = await new PostgresStrengthPlanPreviewSourceV020(context)
            .LoadInCurrentTransactionAsync(userId, cancellationToken).ConfigureAwait(false);
        if (source is null || source.ActivePlanId != activePlanId || source.ActiveRevisionId != revision.Id ||
            source.SavedAnswers is null || source.ProfileVersion is null || source.SelectedActivityTypes is null ||
            source.AvailableWeekdays is null || source.ActivitySessionDurationsMinutes is null ||
            source.RequestedSessionsPerWeek is null || source.ExcludedExerciseIds is null ||
            !source.SelectedActivityTypes.Contains((int)style) || !MatchesAppliedPreferences(draft, source, equipment))
            return WorkoutReconciliationOutcome.NeedsReview;
        var completedToday = await context.WorkoutCompletionDayCredits.AsNoTracking()
            .AnyAsync(row => row.UserId == userId && row.LocalDate == today, cancellationToken).ConfigureAwait(false);
        var acceptance = new PostgresAcceptStrengthPlanProposalStoreV020(context);

        var styleDto = WorkoutActivityContractMapper.ToDto(style);
        var request = new StrengthPlanPreviewRequestV020(styleDto, today, previous.ActivityHistory,
            WarmupMinutes: previous.WarmupMinutes, CooldownMinutes: previous.CooldownMinutes)
        {
            DraftPreferences = draft,
            ExplicitStrengthStyleChoice = styleDto
        };
        var preview = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(userId, request, source, styleDto);
        if (preview.Status != FeasibleStatus || preview.EffectiveActivityTypeId != (int)style ||
            preview.MixedActivitySchedule?.Sessions is not { Count: > 0 }) return WorkoutReconciliationOutcome.NeedsReview;
        var manualCompletedToday = await context.WorkoutLogs.AsNoTracking().AnyAsync(log =>
            log.UserId == userId && log.PlannedSessionId == null && log.Date == today &&
            log.CompletionPercent >= 100 && context.WorkoutDays.Any(day => day.UserId == userId &&
                day.Id == log.WorkoutDayId && day.WorkoutType != nameof(WorkoutType.Rest)), cancellationToken)
            .ConfigureAwait(false);
        manualCompletedToday |= await context.WorkoutSessionLogs.AsNoTracking().AnyAsync(session =>
            session.UserId == userId && !session.IsDeleted && session.PlannedWorkoutDayId == null &&
            session.UnplannedActivityType != null && session.UnplannedCompletionLocalDate == today &&
            session.UnplannedCompletionTimeZoneId != null && session.UnplannedCompletionTimeZoneVersion != null &&
            session.CompletedAt != null, cancellationToken).ConfigureAwait(false);
        int? sameDayStartOrder = null;
        if (state.PromotionPending && !state.SafetyRegenerationPending && completedToday && !manualCompletedToday)
            sameDayStartOrder = await acceptance.FindProvenSameDayPromotionOrderAsync(userId, activePlanId.Value,
                today, preview.MixedActivitySchedule.Sessions, cancellationToken).ConfigureAwait(false);
        var eligibleStart = WorkoutPlanStartDateRule.FindNextEligibleDate(now, zone,
            source.AvailableWeekdays, completedToday || manualCompletedToday);
        var start = state.SafetyRegenerationPending ? eligibleStart :
            await acceptance.FindNextScheduledDateAsync(userId, activePlanId.Value, today,
                (completedToday && sameDayStartOrder is null) || manualCompletedToday, cancellationToken)
                .ConfigureAwait(false) ?? eligibleStart;
        if (start is null || !source.AvailableWeekdays.Contains(start.Value.DayOfWeek))
            return WorkoutReconciliationOutcome.NeedsReview;
        var saved = await new PostgresStrengthPlanProposalStoreV020(context).SaveInCurrentTransactionAsync(userId,
            request, preview, cancellationToken, automaticOrigin: true, automaticStartDate: start).ConfigureAwait(false);
        var result = await acceptance.AcceptInCurrentTransactionAsync(userId,
            new AcceptStrengthPlanProposalRequestV020(saved.ProposalId),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.Automatic, cancellationToken).ConfigureAwait(false);
        if (result.Status != AcceptedStatus)
        {
            var proposal = await context.WorkoutPlanProposalsV02.SingleAsync(row => row.UserId == userId &&
                row.Id == saved.ProposalId, cancellationToken).ConfigureAwait(false);
            if (proposal.Status != AcceptedStatus)
            {
                proposal.Status = ExpiredStatus;
                proposal.DecidedAtUtc = DateTime.UtcNow;
                proposal.ConcurrencyVersion++;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            return WorkoutReconciliationOutcome.NeedsReview;
        }
        state.PromotionPending = false;
        state.SafetyRegenerationPending = false;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return WorkoutReconciliationOutcome.Completed;
    }

    private static bool MatchesAppliedPreferences(WorkoutPreferencesDto draft, StrengthPlanPreviewSourceV020 source,
        Equipment equipment) => draft.ActivityTypes.ToHashSet().SetEquals(source.SelectedActivityTypes!) &&
        draft.AvailableWeekdays.Select(day => (DayOfWeek)day).ToHashSet().SetEquals(source.AvailableWeekdays!) &&
        EqualEntries(draft.ActivitySessionDurationsMinutes, source.ActivitySessionDurationsMinutes!) &&
        EqualEntries(draft.RequestedSessionsPerWeek ?? new Dictionary<int, int>(), source.RequestedSessionsPerWeek!) &&
        (draft.ExcludedExerciseIds ?? new HashSet<int>()).SetEquals(source.ExcludedExerciseIds!) &&
        Enum.TryParse<Equipment>(draft.EquipmentLevel, true, out var draftEquipment) && draftEquipment == equipment;

    private static bool EqualEntries(IReadOnlyDictionary<int, int> left, IReadOnlyDictionary<int, int> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private sealed record AppliedInput(StrengthPlanPreviewRequestV020 Request);

}
