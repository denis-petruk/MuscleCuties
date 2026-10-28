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
        if (await WorkoutSessionAccountLock.HasActivePreviewAsync(context, userId, now.UtcDateTime, cancellationToken).ConfigureAwait(false))
            return WorkoutReconciliationOutcome.Deferred;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return WorkoutReconciliationOutcome.NeedsReview; }
        catch (InvalidTimeZoneException) { return WorkoutReconciliationOutcome.NeedsReview; }
        var today = WorkoutAccountCalendarRule.LocalDate(now, zone);
        var injuries = await context.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var hasInjuryHistory = state.SafetyRegenerationPending &&
            (injuries.Count > 0 || await context.WorkoutInjuryEpisodes.AsNoTracking()
                .AnyAsync(episode => episode.UserId == userId, cancellationToken).ConfigureAwait(false));
        if (injuries.Any(injury => (string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase) &&
                (injury.ClearedDate is null || injury.ClearedDate > today)) ||
            (!string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(injury.Status, "Acute", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(injury.Status, "Recovering", StringComparison.OrdinalIgnoreCase))))
            return WorkoutReconciliationOutcome.NeedsReview;
        var activePlanId = await context.WorkoutPlans.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive).OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is null) return WorkoutReconciliationOutcome.NeedsReview;
        var acceptance = new PostgresAcceptStrengthPlanProposalStoreV020(context);
        var activeSessions = await acceptance.InspectPromotionActiveSessionsAsync(userId, activePlanId.Value, cancellationToken).ConfigureAwait(false);
        if (!activeSessions.Proven || (activeSessions.Any && (state.SafetyRegenerationPending || !state.PromotionPending)))
            return WorkoutReconciliationOutcome.Deferred;
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
        if (state.PromotionPending && !state.SafetyRegenerationPending && (completedToday || activeSessions.Any) && !manualCompletedToday)
            sameDayStartOrder = await acceptance.FindProvenSameDayPromotionOrderAsync(userId, activePlanId.Value,
                today, preview.MixedActivitySchedule.Sessions, cancellationToken).ConfigureAwait(false);
        var eligibleStart = WorkoutPlanStartDateRule.FindNextEligibleDate(now, zone,
            source.AvailableWeekdays, completedToday || manualCompletedToday);
        var start = state.SafetyRegenerationPending ? eligibleStart :
            await acceptance.FindNextScheduledDateAsync(userId, activePlanId.Value, today,
                ((completedToday || activeSessions.Any) && sameDayStartOrder is null) || manualCompletedToday, cancellationToken)
                .ConfigureAwait(false) ?? eligibleStart;
        if (start is null || !source.AvailableWeekdays.Contains(start.Value.DayOfWeek))
            return WorkoutReconciliationOutcome.NeedsReview;
        var activeInjuries = injuries.Any(injury => !string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase));
        var replacement = hasInjuryHistory && activeInjuries
            ? await AutomaticInjuryProvenance.LoadAsync(context, userId, activePlanId.Value, start.Value,
                preview.Sessions, source, cancellationToken).ConfigureAwait(false)
            : null;
        if (activeInjuries && replacement is null) return WorkoutReconciliationOutcome.NeedsReview;
        var restoration = hasInjuryHistory && !activeInjuries
            ? await PrepareExactRestorationAsync(userId, activePlanId.Value, start.Value, preview, source, cancellationToken).ConfigureAwait(false)
            : [];
        if (restoration is null)
        {
            if (!await ProveNoRestorationNeededAsync(userId, activePlanId.Value, start.Value, preview, source,
                    cancellationToken).ConfigureAwait(false)) return WorkoutReconciliationOutcome.NeedsReview;
            restoration = [];
        }
        var transaction = context.Database.CurrentTransaction ?? throw new InvalidOperationException("A planning transaction is required.");
        const string restorationSavepoint = "injury_restoration";
        await transaction.CreateSavepointAsync(restorationSavepoint, cancellationToken).ConfigureAwait(false);
        var saved = await new PostgresStrengthPlanProposalStoreV020(context).SaveInCurrentTransactionAsync(userId,
            request, preview, cancellationToken, automaticOrigin: true, automaticStartDate: start).ConfigureAwait(false);
        var result = await acceptance.AcceptInCurrentTransactionAsync(userId,
            new AcceptStrengthPlanProposalRequestV020(saved.ProposalId),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.Automatic, cancellationToken,
            injuryProvenanceValidated: hasInjuryHistory).ConfigureAwait(false);
        if (result.Status != AcceptedStatus)
        {
            await transaction.RollbackToSavepointAsync(restorationSavepoint, cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
            return WorkoutReconciliationOutcome.NeedsReview;
        }
        if ((restoration.Count > 0 || replacement is not null) && (result.PlanId is not { } newPlan ||
            (restoration.Count > 0 && !await ApplyExactRestorationAsync(userId, newPlan, restoration, cancellationToken).ConfigureAwait(false)) ||
            (replacement is not null && !await ApplyActiveReplacementsAsync(userId, newPlan, replacement, cancellationToken).ConfigureAwait(false))))
        {
            await transaction.RollbackToSavepointAsync(restorationSavepoint, cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
            return WorkoutReconciliationOutcome.NeedsReview;
        }
        state.PromotionPending = false;
        state.SafetyRegenerationPending = false;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return WorkoutReconciliationOutcome.Completed;
    }

    private sealed record ExactRestoration(DayOfWeek Weekday, string Template, string Version, int SessionIndex,
        string Slot, int ExerciseId, int Sets, int Reps, bool OriginallyOptional, IReadOnlyDictionary<DateOnly, Guid> OriginsByDate,
        IReadOnlyList<Guid> EpisodeIds, IReadOnlyList<InjuryRestorationEpisode> Episodes);

    // No lineage transfer is needed only when both old and proposed prescriptions
    // are proven unrelated to every explicitly cleared episode.
    private async Task<bool> ProveNoRestorationNeededAsync(Guid userId, Guid planId, DateOnly start,
        StrengthPlanPreviewResponseV020 preview, StrengthPlanPreviewSourceV020 source, CancellationToken ct)
    {
        var logs = await context.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        var episodes = await context.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        if (logs.Count == 0 || episodes.Count == 0 ||
            logs.Any(log => !string.Equals(log.Status, "Cleared", StringComparison.OrdinalIgnoreCase) || log.ClearedDate is null ||
                episodes.Count(episode => episode.InjuryLogId == log.Id && episode.SiteFlag == log.SiteFlag &&
                    episode.ClearedOn == log.ClearedDate) != 1) ||
            episodes.Any(episode => episode.SourceDeleted || episode.SiteFlag == (int)InjuryFlag.None ||
                !Enum.IsDefined(typeof(InjuryFlag), episode.SiteFlag) || episode.ClearedOn is null || episode.ClearedOn > start ||
                string.IsNullOrWhiteSpace(episode.ClearanceTimeZoneId) || episode.ClearanceTimeZoneVersion is null ||
                logs.Count(log => log.Id == episode.InjuryLogId && log.SiteFlag == episode.SiteFlag &&
                    log.ClearedDate == episode.ClearedOn) != 1)) return false;
        var days = await context.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
            (day.Date == null || day.Date >= start)).ToListAsync(ct).ConfigureAwait(false);
        if (days.Any(day => day.Date is null || day.IsRemovedBySafety || day.SafetyInjuryId is not null ||
            day.SafetyActivityTypeId is not null)) return false;
        var dayIds = days.Select(day => day.Id).ToArray();
        var exercises = await context.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId &&
            dayIds.Contains(row.WorkoutDayId)).ToListAsync(ct).ConfigureAwait(false);
        var exerciseIds = exercises.Select(row => row.Id).ToArray();
        if (exercises.Any(row => row.InjuryOriginalExerciseId is not null || row.OriginWorkoutDayExerciseId is not null) ||
            await context.WorkoutInjuryPrescriptionLinks.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                exerciseIds.Contains(row.WorkoutDayExerciseId), ct).ConfigureAwait(false) ||
            await context.WorkoutInjuryPrescriptions.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                exerciseIds.Contains(row.OriginalWorkoutDayExerciseId), ct).ConfigureAwait(false)) return false;
        var selectedIds = exercises.Select(row => row.ExerciseId)
            .Concat(preview.Sessions.SelectMany(session => session.Slots)
                .Where(slot => slot.ExerciseId.HasValue).Select(slot => slot.ExerciseId!.Value)).Distinct().ToArray();
        foreach (var exerciseId in selectedIds)
        {
            var entries = source.Catalog.Where(entry => entry.Candidate.ExerciseId == exerciseId).ToArray();
            if (entries.Length != 1 || entries[0].MetadataGaps.Count != 0 || entries[0].Candidate.Contraindications is not { } flags ||
                episodes.Any(episode => (flags & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)) return false;
        }
        return true;
    }

    // Only exact, fully linked original prescriptions are admitted here. Alternatives
    // and ambiguous lineage stay pending until their reviewed mapping is available.
    private async Task<List<ExactRestoration>?> PrepareExactRestorationAsync(Guid userId, Guid planId, DateOnly start,
        StrengthPlanPreviewResponseV020 preview, StrengthPlanPreviewSourceV020 source, CancellationToken ct)
    {
        var futureDays = await context.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
            (day.Date == null || day.Date >= start)).ToListAsync(ct).ConfigureAwait(false);
        // An unrelated or unclassified suppression must not disappear as a side effect
        // of accepting an injury restoration.
        if (futureDays.Any(day => day.Date is null || day.SafetyActivityTypeId is not null ||
            (day.IsRemovedBySafety && day.SafetyInjuryId is null) ||
            (day.IsRemovedBySafety && day.WorkoutType != nameof(WorkoutType.Strength)) ||
            (!day.IsRemovedBySafety && day.SafetyInjuryId is not null))) return null;
        var days = futureDays.Where(day => day.WorkoutType == nameof(WorkoutType.Strength)).ToList();
        if (days.Count == 0) return null;
        var otherDayIds = futureDays.Where(day => day.WorkoutType != nameof(WorkoutType.Strength)).Select(day => day.Id).ToArray();
        var otherExercises = await context.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId &&
            otherDayIds.Contains(row.WorkoutDayId)).ToListAsync(ct).ConfigureAwait(false);
        var otherExerciseIds = otherExercises.Select(row => row.Id).ToArray();
        if (otherExercises.Any(row => row.InjuryOriginalExerciseId is not null || row.OriginWorkoutDayExerciseId is not null) ||
            await context.WorkoutInjuryPrescriptionLinks.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                otherExerciseIds.Contains(row.WorkoutDayExerciseId), ct).ConfigureAwait(false) ||
            await context.WorkoutInjuryPrescriptions.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                otherExerciseIds.Contains(row.OriginalWorkoutDayExerciseId), ct).ConfigureAwait(false)) return null;
        var dayIds = days.Select(day => day.Id).ToArray();
        if (days.Any(day => day.SafetyActivityTypeId is not null) ||
            await context.WorkoutLogs.AsNoTracking().AnyAsync(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId), ct).ConfigureAwait(false) ||
            await context.WorkoutSessionLogs.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                row.PlannedWorkoutDayId != null && dayIds.Contains(row.PlannedWorkoutDayId.Value), ct).ConfigureAwait(false)) return null;
        var exercises = await context.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId &&
            dayIds.Contains(row.WorkoutDayId)).ToListAsync(ct).ConfigureAwait(false);
        if (days.Any(day => day.IsRemovedBySafety && exercises.All(row => row.WorkoutDayId != day.Id))) return null;
        var exerciseIds = exercises.Select(row => row.Id).ToArray();
        var links = await context.WorkoutInjuryPrescriptionLinks.AsNoTracking().Where(row => row.UserId == userId &&
            exerciseIds.Contains(row.WorkoutDayExerciseId)).ToListAsync(ct).ConfigureAwait(false);
        var originals = await context.WorkoutInjuryPrescriptions.AsNoTracking().Where(row => row.UserId == userId &&
            exerciseIds.Contains(row.OriginalWorkoutDayExerciseId)).ToListAsync(ct).ConfigureAwait(false);
        var episodes = await context.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId)
            .ToDictionaryAsync(row => row.Id, ct).ConfigureAwait(false);
        var injuryLogs = await context.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        if (injuryLogs.Any(log => episodes.Values.Count(episode => episode.InjuryLogId == log.Id &&
                episode.SiteFlag == log.SiteFlag && episode.ClearedOn == log.ClearedDate) != 1) ||
            episodes.Values.Any(episode => injuryLogs.Count(log => log.Id == episode.InjuryLogId &&
                log.SiteFlag == episode.SiteFlag && log.ClearedDate == episode.ClearedOn) != 1) ||
            episodes.Values.Any(episode => episode.SourceDeleted || episode.SiteFlag == (int)InjuryFlag.None ||
                !Enum.IsDefined(typeof(InjuryFlag), episode.SiteFlag) || episode.ClearedOn is null || episode.ClearedOn > start ||
                string.IsNullOrWhiteSpace(episode.ClearanceTimeZoneId) || episode.ClearanceTimeZoneVersion is null)) return null;
        if (days.Any(day => day.IsRemovedBySafety && episodes.Values.All(episode => episode.InjuryLogId != day.SafetyInjuryId)))
            return null;
        var result = new List<ExactRestoration>();
        foreach (var exercise in exercises)
        {
            var day = days.Single(row => row.Id == exercise.WorkoutDayId);
            if (day.Date is not { } scheduledDate) return null;
            var dayLinks = links.Where(link => link.WorkoutDayExerciseId == exercise.Id).ToArray();
            var snapshots = originals.Where(row => row.OriginalWorkoutDayExerciseId == exercise.Id).ToArray();
            var candidateId = exercise.InjuryOriginalExerciseId ?? exercise.ExerciseId;
            var knownCandidate = source.Catalog.Where(entry => entry.Candidate.ExerciseId == candidateId).ToArray();
            if (knownCandidate.Length != 1 || knownCandidate[0].MetadataGaps.Count != 0 ||
                knownCandidate[0].Candidate.Contraindications is null) return null;
            var expectedEpisodeIds = episodes.Values.Where(episode =>
                (knownCandidate[0].Candidate.Contraindications!.Value & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)
                .Select(episode => episode.Id).Order().ToArray();
            if (expectedEpisodeIds.Length == 0)
            {
                if (dayLinks.Length != 0 || snapshots.Length != 0 || exercise.InjuryOriginalExerciseId is not null) return null;
                continue;
            }
            if (!expectedEpisodeIds.SequenceEqual(dayLinks.Select(link => link.InjuryEpisodeId).Order())) return null;
            if (exercise.PlanningTemplateId is null || exercise.PlanningTemplateVersion is null ||
                exercise.PlanningSlotId is null || exercise.PlanningSessionIndex is null || dayLinks.Length == 0 ||
                dayLinks.Select(link => link.InjuryEpisodeId).Distinct().Count() != dayLinks.Length ||
                snapshots.Length != dayLinks.Length || snapshots.Select(snapshot => snapshot.EpisodeId).Distinct().Count() != snapshots.Length ||
                dayLinks.Any(link => !episodes.ContainsKey(link.InjuryEpisodeId)) ||
                dayLinks.Any(link => episodes[link.InjuryEpisodeId].SourceDeleted || episodes[link.InjuryEpisodeId].ClearedOn is null ||
                    !snapshots.Any(snapshot => snapshot.EpisodeId == link.InjuryEpisodeId &&
                        snapshot.OriginalExerciseId == link.OriginalExerciseId))) return null;
            var original = snapshots[0];
            if (original.OriginalExerciseId != candidateId || snapshots.Any(snapshot => snapshot.WorkoutDayId != day.Id ||
                snapshot.OriginalOriginWorkoutDayExerciseId != original.OriginalOriginWorkoutDayExerciseId ||
                snapshot.OriginalExerciseId != original.OriginalExerciseId ||
                snapshot.OriginalPlanningSlotId != exercise.PlanningSlotId || snapshot.OriginalSets != original.OriginalSets ||
                snapshot.OriginalReps != original.OriginalReps || snapshot.OriginalOptionalSets != original.OriginalOptionalSets ||
                snapshot.OriginalDurationSeconds != original.OriginalDurationSeconds || snapshot.OriginalIsOptional != original.OriginalIsOptional ||
                snapshot.OriginalIsRemovedByReadiness) || original.OriginalOptionalSets != 0 || original.OriginalDurationSeconds is not null)
                return null;
            var sessions = preview.Sessions.Where(session => session.PlannedWeekday == scheduledDate.DayOfWeek &&
                session.TemplateId == exercise.PlanningTemplateId && session.TemplateVersion == exercise.PlanningTemplateVersion &&
                session.SessionIndex == exercise.PlanningSessionIndex).ToArray();
            if (sessions.Length != 1) return null;
            var slots = sessions[0].Slots.Where(slot => slot.SlotId == exercise.PlanningSlotId).ToArray();
            var entries = source.Catalog.Where(entry => entry.Candidate.ExerciseId == original.OriginalExerciseId).ToArray();
            if (slots.Length != 1 || slots[0].ExerciseId != original.OriginalExerciseId || slots[0].Sets != original.OriginalSets ||
                slots[0].MinimumRepetitions != original.OriginalReps || entries.Length != 1 || entries[0].MetadataGaps.Count != 0 ||
                entries[0].Candidate.Contraindications is not { } contraindications ||
                episodes.Values.Any(episode => (contraindications & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None &&
                    dayLinks.All(link => link.InjuryEpisodeId != episode.Id))) return null;
            var episodeEvidence = dayLinks.Select(link => new InjuryRestorationEpisode(false, episodes[link.InjuryEpisodeId].ClearedOn)).ToArray();
            if (WorkoutInjuryClearanceRule.EvaluateRestoration(original.OriginalIsOptional, true,
                    episodeEvidence, scheduledDate) == InjuryClearanceRestorationDisposition.Blocked)
                return null;
            result.Add(new ExactRestoration(scheduledDate.DayOfWeek, exercise.PlanningTemplateId, exercise.PlanningTemplateVersion,
                exercise.PlanningSessionIndex.Value, exercise.PlanningSlotId, original.OriginalExerciseId, original.OriginalSets,
                original.OriginalReps, original.OriginalIsOptional, new Dictionary<DateOnly, Guid> { [scheduledDate] = original.OriginalOriginWorkoutDayExerciseId ?? exercise.Id },
                dayLinks.Select(link => link.InjuryEpisodeId).Order().ToArray(), episodeEvidence));
        }
        if (result.Count == 0) return null;
        // Repeated weekly occurrences must agree on origin and episode coverage.
        if (result.GroupBy(item => (item.Weekday, item.Template, item.Version, item.SessionIndex, item.Slot))
            .Any(group => group.Any(item => item.ExerciseId != group.First().ExerciseId || item.Sets != group.First().Sets ||
                item.Reps != group.First().Reps || item.OriginallyOptional != group.First().OriginallyOptional ||
                !item.EpisodeIds.SequenceEqual(group.First().EpisodeIds)))) return null;
        var groups = result.GroupBy(item => (item.Weekday, item.Template, item.Version, item.SessionIndex, item.Slot)).ToArray();
        if (groups.Any(group => group.SelectMany(item => item.OriginsByDate.Keys).Distinct().Count() != group.Count())) return null;
        return groups.Select(group => group.First() with
            { OriginsByDate = group.SelectMany(item => item.OriginsByDate).ToDictionary(pair => pair.Key, pair => pair.Value) }).ToList();
    }

    private async Task<bool> ApplyActiveReplacementsAsync(Guid userId, Guid planId,
        AutomaticInjuryProvenance.Evidence evidence, CancellationToken ct)
    {
        var days = await context.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
            day.Date != null).ToListAsync(ct).ConfigureAwait(false);
        var ids = days.Select(day => day.Id).ToArray();
        var exercises = await context.WorkoutDayExercises.Where(row => row.UserId == userId && ids.Contains(row.WorkoutDayId))
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var slot in evidence.Slots)
        {
            var matchingDays = days.Where(day => day.Date!.Value.DayOfWeek == slot.Identity.Weekday &&
                day.WorkoutType == nameof(WorkoutType.Strength)).ToArray();
            var matches = exercises.Where(row => matchingDays.Any(day => day.Id == row.WorkoutDayId) &&
                row.PlanningTemplateId == slot.Identity.TemplateId && row.PlanningTemplateVersion == slot.Identity.TemplateVersion &&
                row.PlanningSessionIndex == slot.Identity.SessionIndex && row.PlanningSlotId == slot.Identity.SlotId &&
                row.ExerciseId == slot.AlternativeExerciseId).ToArray();
            if (matches.Length == 0 || matches.Length != matchingDays.Length ||
                matches.Select(row => row.WorkoutDayId).Distinct().Count() != matches.Length) return false;
            foreach (var exercise in matches)
            {
                var day = matchingDays.Single(value => value.Id == exercise.WorkoutDayId);
                // A matching date retains its own origin. The final recurrence can
                // extend beyond the old horizon; the preflight proved every source
                // occurrence has the same original dose, slot and causal episodes.
                var originId = slot.OriginsByDate.TryGetValue(day.Date!.Value, out var exactOrigin)
                    ? exactOrigin : slot.OriginsByDate.OrderBy(pair => pair.Key).Last().Value;
                if (exercise.InjuryOriginalExerciseId is not null || exercise.OriginWorkoutDayExerciseId is not null) return false;
                exercise.InjuryOriginalExerciseId = slot.OriginalExerciseId;
                exercise.OriginWorkoutDayExerciseId = originId;
                foreach (var episodeId in slot.EpisodeIds)
                {
                    context.WorkoutInjuryPrescriptionLinks.Add(new MuscleCuties.Infrastructure.PostgreSql.Models.App.WorkoutInjuryPrescriptionLinkRecord
                    { Id = Guid.NewGuid(), UserId = userId, WorkoutDayExerciseId = exercise.Id, InjuryEpisodeId = episodeId, OriginalExerciseId = slot.OriginalExerciseId });
                    context.WorkoutInjuryPrescriptions.Add(new MuscleCuties.Infrastructure.PostgreSql.Models.App.WorkoutInjuryPrescriptionRecord
                    {
                        Id = Guid.NewGuid(), UserId = userId, EpisodeId = episodeId, WorkoutDayId = exercise.WorkoutDayId,
                        OriginalWorkoutDayExerciseId = exercise.Id, OriginalExerciseId = slot.OriginalExerciseId,
                        OriginalPlanningSlotId = slot.Identity.SlotId, OriginalOriginWorkoutDayExerciseId = originId,
                        OriginalSets = slot.Sets, OriginalOptionalSets = slot.OptionalSets, OriginalReps = slot.Reps,
                        OriginalDurationSeconds = slot.DurationSeconds, OriginalIsOptional = slot.IsOptional,
                        OriginalIsRemovedByReadiness = slot.IsRemovedByReadiness,
                        ReplacementWorkoutDayExerciseId = exercise.Id, ReplacementExerciseId = slot.AlternativeExerciseId
                    });
                }
            }
        }
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ApplyExactRestorationAsync(Guid userId, Guid planId, IReadOnlyCollection<ExactRestoration> mapping,
        CancellationToken ct)
    {
        var episodeIds = mapping.SelectMany(item => item.EpisodeIds).Distinct().ToArray();
        var episodes = await context.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId && episodeIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, ct).ConfigureAwait(false);
        var days = await context.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId && day.Date != null)
            .ToListAsync(ct).ConfigureAwait(false);
        var ids = days.Select(day => day.Id).ToArray();
        var exercises = await context.WorkoutDayExercises.Where(row => row.UserId == userId && ids.Contains(row.WorkoutDayId))
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in mapping)
        {
            var matchingDays = days.Where(day => day.Date!.Value.DayOfWeek == item.Weekday &&
                day.WorkoutType == nameof(WorkoutType.Strength)).ToArray();
            var matches = exercises.Where(row => matchingDays.Any(day => day.Id == row.WorkoutDayId) &&
                row.PlanningTemplateId == item.Template && row.PlanningTemplateVersion == item.Version &&
                row.PlanningSessionIndex == item.SessionIndex && row.PlanningSlotId == item.Slot && row.ExerciseId == item.ExerciseId).ToArray();
            if (matches.Length == 0 || matches.Length != matchingDays.Length) return false;
            foreach (var exercise in matches)
            {
                var day = matchingDays.Single(value => value.Id == exercise.WorkoutDayId);
                var originId = item.OriginsByDate.TryGetValue(day.Date!.Value, out var exactOrigin)
                    ? exactOrigin : item.OriginsByDate.OrderBy(pair => pair.Key).Last().Value;
                if (exercise.Sets != item.Sets || exercise.Reps != item.Reps) return false;
                if (item.EpisodeIds.Any(id => !episodes.ContainsKey(id)) ||
                    WorkoutInjuryClearanceRule.EvaluateRestoration(item.OriginallyOptional, true,
                        item.EpisodeIds.Select(id => new InjuryRestorationEpisode(episodes[id].ClearedOn is null, episodes[id].ClearedOn)).ToArray(),
                        day.Date!.Value) == InjuryClearanceRestorationDisposition.Blocked)
                    return false;
                var disposition = WorkoutInjuryClearanceRule.EvaluateRestoration(item.OriginallyOptional, true, item.Episodes, day.Date!.Value);
                if (disposition == InjuryClearanceRestorationDisposition.Blocked) return false;
                exercise.IsOptional = disposition == InjuryClearanceRestorationDisposition.RestoreOptional;
                exercise.InjuryOriginalExerciseId = item.ExerciseId;
                exercise.OriginWorkoutDayExerciseId = originId;
                exercise.InjuryRestorationRequiredFromDate = item.OriginallyOptional ? null :
                    WorkoutInjuryClearanceRule.RequiredFromForAllClearances(
                        item.Episodes.Select(episode => episode.ClearedOn).ToArray());
                foreach (var episodeId in item.EpisodeIds)
                {
                    context.WorkoutInjuryPrescriptionLinks.Add(new MuscleCuties.Infrastructure.PostgreSql.Models.App.WorkoutInjuryPrescriptionLinkRecord
                    { Id = Guid.NewGuid(), UserId = userId, WorkoutDayExerciseId = exercise.Id, InjuryEpisodeId = episodeId, OriginalExerciseId = item.ExerciseId });
                    context.WorkoutInjuryPrescriptions.Add(new MuscleCuties.Infrastructure.PostgreSql.Models.App.WorkoutInjuryPrescriptionRecord
                    {
                        Id = Guid.NewGuid(), UserId = userId, EpisodeId = episodeId, WorkoutDayId = exercise.WorkoutDayId,
                        OriginalWorkoutDayExerciseId = exercise.Id, OriginalExerciseId = item.ExerciseId,
                        OriginalPlanningSlotId = item.Slot, OriginalOriginWorkoutDayExerciseId = originId,
                        OriginalSets = item.Sets, OriginalOptionalSets = 0, OriginalReps = item.Reps,
                        OriginalDurationSeconds = null, OriginalIsOptional = item.OriginallyOptional,
                        OriginalIsRemovedByReadiness = false
                    });
                }
            }
        }
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
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

}
