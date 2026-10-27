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

/// <summary>Applies eligible promotion revisions under the caller-owned tenant transaction and account lock.</summary>
public sealed class PostgresWorkoutPlanReconciler(PostgresDbContext context)
{
    private const int RequiredCurrentStrengthStyleCount = 1;
    private const string FeasibleStatus = "Feasible";
    private const string AcceptedStatus = "Accepted";
    private const string ExpiredStatus = "Expired";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    private const string ProposedStatus = "Proposed";
    private const string FeasibleStatus = "Feasible";
    private const string AcceptedStatus = "Accepted";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkoutReconciliationOutcome> ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (state is null || (!state.PromotionPending && !state.SafetyRegenerationPending) || state.TimeZoneId is null)
            return;
        var now = DateTimeOffset.UtcNow;
        if (await WorkoutSessionAccountLock.HasActivePreviewAsync(context, userId, now.UtcDateTime, cancellationToken).ConfigureAwait(false) ||
            await context.WorkoutSessionLogs.AnyAsync(row => row.UserId == userId && !row.IsDeleted && row.CompletedAt == null,
                cancellationToken).ConfigureAwait(false))
            return;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return; }
        catch (InvalidTimeZoneException) { return; }
        var today = WorkoutAccountCalendarRule.LocalDate(now, zone);
        var injuries = await context.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        // Injury revisions require restoration provenance; do not consume the shared pending flag here.
        if (state.SafetyRegenerationPending &&
            (injuries.Count > 0 || await context.WorkoutInjuryEpisodes.AsNoTracking()
                .AnyAsync(episode => episode.UserId == userId, cancellationToken).ConfigureAwait(false)))
            return;
        if (injuries.Any(injury => !string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase) ||
            injury.ClearedDate is null || WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(injury.ClearedDate.Value, today)))
            return;
        var activePlanId = await context.WorkoutPlans.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive).OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is null) return;
        var styleIds = await context.WorkoutDays.AsNoTracking()
            .Where(row => row.UserId == userId && row.WorkoutPlanId == activePlanId && row.ActivityTypeId != null)
            .Select(row => row.ActivityTypeId!.Value).Distinct().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var styles = styleIds.Where(id => Enum.IsDefined((WorkoutActivityType)id) &&
            StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)id)).ToArray();
        if (styles.Length != RequiredCurrentStrengthStyleCount) return;
        var style = (WorkoutActivityType)styles[0];
        var profile = await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null || !Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var equipment) || !Enum.IsDefined(equipment)) return;
        var revision = await context.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(row => row.UserId == userId && row.PlanId == activePlanId)
            .OrderByDescending(row => row.RevisionNumber).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (revision is null) return;
        AppliedInput? input;
        try { input = JsonSerializer.Deserialize<AppliedInput>(revision.InputSnapshotJson, JsonOptions); }
        catch (JsonException) { return; }
        var previous = input?.Request;
        var draft = previous?.DraftPreferences;
        if (draft is null || previous!.WarmupMinutes is null || previous.CooldownMinutes is null ||
            !WorkoutPreferencesRule.IsValid(draft.ActivityTypes, draft.AvailableWeekdays,
                draft.ActivitySessionDurationsMinutes, draft.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(draft.EquipmentLevel)) return;

        // Catalog serialization is needed only after account state proves this read can publish a plan.
        await PlanningCatalogFingerprint.LockCatalogAsync(context, cancellationToken).ConfigureAwait(false);
        var source = await new PostgresStrengthPlanPreviewSourceV020(context)
            .LoadInCurrentTransactionAsync(userId, cancellationToken).ConfigureAwait(false);
        if (source is null || source.ActivePlanId != activePlanId || source.ActiveRevisionId != revision.Id ||
            source.SavedAnswers is null || source.ProfileVersion is null || source.SelectedActivityTypes is null ||
            source.AvailableWeekdays is null || source.ActivitySessionDurationsMinutes is null ||
            source.RequestedSessionsPerWeek is null || source.ExcludedExerciseIds is null ||
            !source.SelectedActivityTypes.Contains((int)style) || !MatchesAppliedPreferences(draft, source, equipment))
            return;
        var completedToday = await context.WorkoutCompletionDayCredits.AsNoTracking()
            .AnyAsync(row => row.UserId == userId && row.LocalDate == today, cancellationToken).ConfigureAwait(false);
        var acceptance = new PostgresAcceptStrengthPlanProposalStoreV020(context);
        var eligibleStart = WorkoutPlanStartDateRule.FindNextEligibleDate(now, zone,
            source.AvailableWeekdays, completedToday);
        var start = state.SafetyRegenerationPending ? eligibleStart :
            await acceptance.FindNextScheduledDateAsync(userId, activePlanId.Value, today, completedToday,
                cancellationToken).ConfigureAwait(false) ?? eligibleStart;
        if (start is null || !source.AvailableWeekdays.Contains(start.Value.DayOfWeek)) return;
        var styleDto = WorkoutActivityContractMapper.ToDto(style);
        var request = new StrengthPlanPreviewRequestV020(styleDto, today, previous.ActivityHistory,
            WarmupMinutes: previous.WarmupMinutes, CooldownMinutes: previous.CooldownMinutes)
        {
            DraftPreferences = draft,
            ExplicitStrengthStyleChoice = styleDto
        };
        var preview = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(userId, request, source, styleDto);
        if (preview.Status != FeasibleStatus || preview.EffectiveActivityTypeId != (int)style ||
            preview.MixedActivitySchedule?.Sessions is not { Count: > 0 }) return;
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
            return;
        }
        state.PromotionPending = false;
        state.SafetyRegenerationPending = false;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
            if (result.PlanId is not { } newPlanId || !await RestoreProvenInjuryPrescriptionsAsync(
                    userId, activePlanId.Value, newPlanId, request.LocalDate, cancellationToken).ConfigureAwait(false))
                return;
            state.PromotionPending = false;
            state.SafetyRegenerationPending = false;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            outcome = WorkoutReconciliationOutcome.Completed;
        }).ConfigureAwait(false);
        return outcome;
    }

    // A canonical preview proves eligibility only for the selected, unchanged prescription.
    // Any ambiguity leaves the transaction uncommitted and the pending event intact.
    private async Task<bool> RestoreProvenInjuryPrescriptionsAsync(Guid userId, Guid previousPlanId,
        Guid newPlanId, DateOnly today, CancellationToken cancellationToken)
    {
        var affectedDays = await dbContext.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId &&
                day.WorkoutPlanId == previousPlanId && day.Date >= today && day.IsRemovedBySafety && day.SafetyInjuryId != null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (affectedDays.Count == 0) return true;
        var dayIds = affectedDays.Select(day => day.Id).ToArray();
        var originals = await dbContext.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId &&
            dayIds.Contains(row.WorkoutDayId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var associations = await dbContext.WorkoutInjuryPrescriptions.Where(row => row.UserId == userId &&
            dayIds.Contains(row.WorkoutDayId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var episodeIds = associations.Select(row => row.EpisodeId).Distinct().ToArray();
        var episodes = await dbContext.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId &&
            episodeIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken).ConfigureAwait(false);
        var newDays = await dbContext.WorkoutDays.AsNoTracking().Where(row => row.UserId == userId &&
            row.WorkoutPlanId == newPlanId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var newDayIds = newDays.Select(row => row.Id).ToArray();
        var replacements = await dbContext.WorkoutDayExercises.Where(row => row.UserId == userId &&
            newDayIds.Contains(row.WorkoutDayId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var day in affectedDays)
        {
            if (day.Date is not { } effectiveDate) return false;
            var dayOriginals = originals.Where(row => row.WorkoutDayId == day.Id).ToArray();
            if (dayOriginals.Length == 0) return false;
            var matchingDays = newDays.Where(row => row.Date == day.Date && row.ActivityTypeId == day.ActivityTypeId).ToArray();
            if (matchingDays.Length != 1) return false;
            foreach (var original in dayOriginals)
            {
                var links = associations.Where(row => row.OriginalWorkoutDayExerciseId == original.Id).ToArray();
                if (links.Length == 0 || string.IsNullOrWhiteSpace(original.PlanningSlotId) || links.Any(link =>
                    link.OriginalPlanningSlotId != original.PlanningSlotId || link.OriginalExerciseId != original.ExerciseId ||
                    !episodes.ContainsKey(link.EpisodeId)) ||
                    !links.Any(link => episodes[link.EpisodeId].InjuryLogId == day.SafetyInjuryId)) return false;
                var matches = replacements.Where(row => row.WorkoutDayId == matchingDays[0].Id &&
                    row.PlanningSlotId == original.PlanningSlotId && row.ExerciseId == original.ExerciseId).ToArray();
                if (matches.Length != 1) return false;
                var replacement = matches[0];
                // Reusing a different dose would bypass canonical timing and volume validation.
                if (links.Any(link => link.OriginalSets != replacement.Sets || link.OriginalReps != replacement.Reps ||
                    link.OriginalDurationSeconds != replacement.DurationSeconds || link.OriginalOptionalSets != replacement.OptionalSets))
                    return false;
                var disposition = WorkoutInjuryClearanceRule.EvaluateRestoration(
                    links.Any(link => link.OriginalIsOptional), true,
                    links.Select(link => new InjuryRestorationEpisode(episodes[link.EpisodeId].ClearedOn is null,
                        episodes[link.EpisodeId].ClearedOn)).ToArray(), effectiveDate);
                if (disposition == InjuryRestorationDisposition.Blocked) return false;
                replacement.IsOptional = disposition == InjuryRestorationDisposition.RestoreOptional;
                replacement.IsRemovedByReadiness = links.Any(link => link.OriginalIsRemovedByReadiness);
                replacement.OriginWorkoutDayExerciseId = original.OriginWorkoutDayExerciseId ?? original.Id;
                foreach (var link in links)
                {
                    link.ReplacementWorkoutDayExerciseId = replacement.Id;
                    link.ReplacementExerciseId = replacement.ExerciseId;
                }
            }
        }
        return true;
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

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private sealed record ProposalInputSnapshot(StrengthPlanPreviewRequestV020 Request);
}
