using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Accepts an immutable feasible proposal and publishes the selected weekly split.</summary>
public sealed class PostgresAcceptStrengthPlanProposalStoreV020(PostgresDbContext dbContext)
    : IAcceptStrengthPlanProposalStoreV020
{
    private const int OverviewScheduleWeeks = 4;
    private const int DaysPerWeek = 7;
    private const long InitialRevisionNumber = 1;
    private const int MinimumSessionIndex = 0;
    private const int MinimumExerciseSetCount = 1;
    private const int MinimumRepetitionCount = 1;
    private const int MondayWeekOrderOffset = 1;
    private const decimal MinimumPlannedActivityDurationMinutes = 0m;
    private const string PlannedDurationMinuteFormat = "0.#";
    private const string MixedActivityPlanName = "Mixed activity plan";
    private const string RestDayName = "Rest day";
    private const string MixedScheduleInvalidCode = "ProposalMixedScheduleInvalid";
    private const string UnsupportedOtherActivityCode = "WorkoutTypeCannotRepresentOtherActivity";
    private const string ConflictStatus = "Conflict";
    private const string StaleStatus = "Stale";
    private const string AcceptedStatus = "Accepted";
    private const string NotFoundStatus = "NotFound";
    private const string InvalidProposalStatus = "InvalidProposal";
    private const string LimitedStatus = "Limited";
    private const string ProposedStatus = "Proposed";
    private const string ExpiredStatus = "Expired";
    private const string FeasibleStatus = "Feasible";

    private const string ProposalNotFoundCode = "ProposalNotFound";
    private const string ProposalAlreadyDecidedCode = "ProposalAlreadyDecided";
    private const string AutomaticProposalNotUserAcceptableCode = "AutomaticProposalNotUserAcceptable";
    private const string PreviewLeaseExpiredOrInvalidCode = "PreviewLeaseExpiredOrInvalid";
    private const string ProposalSnapshotInvalidCode = "ProposalSnapshotInvalid";
    private const string PlanningInputsChangedSinceProposalCode = "PlanningInputsChangedSinceProposal";
    private const string CalendarNotEstablishedCode = "CalendarNotEstablished";
    private const string ActivePlanChangedSinceProposalCode = "ActivePlanChangedSinceProposal";
    private const string ReplacementConfirmationRequiredCode = "ReplacementConfirmationRequired";
    private const string ReducedVolumeAcknowledgmentRequiredCode = "ReducedVolumeAcknowledgmentRequired";
    private const string WorkoutPreferencesChangedSinceProposalCode = "WorkoutPreferencesChangedSinceProposal";
    private const string DraftPreferencesInvalidCode = "DraftPreferencesInvalid";
    private const string ExerciseMuscleMetadataChangedCode = "ExerciseMuscleMetadataChanged";
    private const string ProposalScheduleSafetyInvalidCode = "ProposalScheduleSafetyInvalid";
    private const string PlanningDateChangedCode = "PlanningDateChanged";
    private const string NoEligibleWorkoutDayCode = "NoEligibleWorkoutDay";

    internal enum AcceptanceOrigin { User, Automatic }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var result = await AcceptInCurrentTransactionAsync(userId, request, AcceptanceOrigin.User, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);

    // Caller owns the tenant transaction and account/catalog locks and commits the result.
    internal async Task<AcceptStrengthPlanProposalResultV020> AcceptInCurrentTransactionAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request, AcceptanceOrigin origin, CancellationToken cancellationToken,
        bool injuryProvenanceValidated = false)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("The active planning transaction is required.");
        if (origin is not AcceptanceOrigin.User and not AcceptanceOrigin.Automatic)
            return new(InvalidProposalStatus, null, null, ProposalSnapshotInvalidCode);
        var proposal = await dbContext.WorkoutPlanProposalsV02.SingleOrDefaultAsync(
                item => item.UserId == userId && item.Id == request.ProposalId,
                cancellationToken)
            .ConfigureAwait(false);
        if (proposal is null)
            return new(NotFoundStatus, null, null, ProposalNotFoundCode);
        var inputSnapshot = Deserialize<ProposalInputSnapshot>(proposal.InputSnapshotJson);
        if (inputSnapshot is not null && inputSnapshot.AutomaticOrigin != (origin == AcceptanceOrigin.Automatic))
            return new(InvalidProposalStatus, null, null, AutomaticProposalNotUserAcceptableCode);
        if (origin == AcceptanceOrigin.Automatic && (inputSnapshot is null ||
            proposal.LeaseId is not null || proposal.LeaseExpiresAtUtc is not null ||
            request.LeaseId != Guid.Empty || request.Fence != 0 || request.ProposalVersion != 0 ||
            request.ReplacementConfirmed || request.ReducedVolumeAcknowledged))
            return new(InvalidProposalStatus, null, null, ProposalSnapshotInvalidCode);
        // A response can be lost after commit. Replaying the same accepted proposal
        // returns its original result without creating another plan or revision.
        if (proposal.Status == AcceptedStatus && proposal.AcceptedRevisionId is { } acceptedRevisionId)
            return new(AcceptedStatus, proposal.PlanId, acceptedRevisionId, null);
        if (proposal.Status != ProposedStatus)
            return new(ConflictStatus, proposal.PlanId, null,
                ProposalAlreadyDecidedCode);
        if (origin == AcceptanceOrigin.User && (proposal.LeaseId != request.LeaseId || proposal.LeaseFence != request.Fence ||
            proposal.ConcurrencyVersion != request.ProposalVersion || proposal.LeaseExpiresAtUtc is null ||
            proposal.LeaseExpiresAtUtc <= DateTime.UtcNow))
            return new(StaleStatus, proposal.PlanId, null,
                PreviewLeaseExpiredOrInvalidCode);

        var proposalSnapshot = Deserialize<ProposalSnapshot>(proposal.ProposalJson);
        if (proposalSnapshot is null || inputSnapshot is null ||
            proposalSnapshot.Status != FeasibleStatus || proposalSnapshot.Sessions is null ||
            proposalSnapshot.PolicyId != StrengthPlanPreviewRule.PolicyId ||
            proposalSnapshot.PolicyVersion != StrengthPlanPreviewRule.Version ||
            proposalSnapshot.RulesetVersion != StrengthPlanPreviewRule.RulesetVersion)
            return new(InvalidProposalStatus, null, null,
                ProposalSnapshotInvalidCode);

        var currentLogs = await dbContext.WorkoutExerciseLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.CompletedSets > 0 && row.CompletedReps > 0)
            .Select(row => new { row.ExerciseId, row.WorkoutLogId, row.WeightKg })
            .OrderBy(row => row.WorkoutLogId).ThenBy(row => row.ExerciseId).ThenBy(row => row.WeightKg)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var currentInjuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (proposalSnapshot.SourceHistoryFingerprint != PlanningInputFingerprint.Hash(currentLogs) ||
            proposalSnapshot.SourceInjuryFingerprint != PlanningInputFingerprint.Injuries(currentInjuries) ||
            string.IsNullOrWhiteSpace(proposalSnapshot.SourceCatalogFingerprint) ||
            proposalSnapshot.SourceCatalogFingerprint != await PlanningCatalogFingerprint.LoadAsync(dbContext, userId, cancellationToken).ConfigureAwait(false))
            return new(StaleStatus, proposal.PlanId, null,
                PlanningInputsChangedSinceProposalCode);
        var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (origin == AcceptanceOrigin.Automatic &&
            (planningState is null || (!planningState.PromotionPending && !planningState.SafetyRegenerationPending)))
            return new(StaleStatus, proposal.PlanId, null, PlanningInputsChangedSinceProposalCode);
        // Injury publication requires the reconciler's preflight and savepoint-owned lineage transfer.
        if (origin == AcceptanceOrigin.Automatic && planningState!.SafetyRegenerationPending && !injuryProvenanceValidated &&
            (currentInjuries.Count > 0 || await dbContext.WorkoutInjuryEpisodes.AsNoTracking()
                .AnyAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false)))
            return new(StaleStatus, proposal.PlanId, null, PlanningInputsChangedSinceProposalCode);
        if (planningState?.TimeZoneId is not { } zoneId)
            return new(LimitedStatus, null, null, CalendarNotEstablishedCode);
        if (proposalSnapshot.SourceTimeZoneVersion != planningState.TimeZoneVersion ||
            proposalSnapshot.SourceExperienceVersion != planningState.ExperienceVersion ||
            proposalSnapshot.SourceAnswerVersion != planningState.AnswerVersion)
            return new(StaleStatus, proposal.PlanId, null,
                PlanningInputsChangedSinceProposalCode);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { return new(LimitedStatus, null, null, CalendarNotEstablishedCode); }
        catch (InvalidTimeZoneException) { return new(LimitedStatus, null, null, CalendarNotEstablishedCode); }

        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId && plan.IsActive)
            .OrderByDescending(plan => plan.CreatedAt)
            .Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var activeRevisionId = activePlanId is { } existingPlanId
            ? await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                .Where(revision => revision.UserId == userId && revision.PlanId == existingPlanId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .Select(revision => (Guid?)revision.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;
        if (activePlanId != inputSnapshot.BaselineActivePlanId ||
            activeRevisionId != inputSnapshot.BaselineRevisionId)
        {
            proposal.Status = ExpiredStatus;
            proposal.DecidedAtUtc = DateTime.UtcNow;
            proposal.ConcurrencyVersion++;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new(StaleStatus, proposal.PlanId, null,
                ActivePlanChangedSinceProposalCode);
        }


        if (origin == AcceptanceOrigin.User && proposalSnapshot.RequiresReducedVolumeAcknowledgment && !request.ReducedVolumeAcknowledged)
            return new(ConflictStatus, proposal.PlanId, null,
                ReducedVolumeAcknowledgmentRequiredCode);

        if (origin == AcceptanceOrigin.User && activePlanId is not null && !request.ReplacementConfirmed)
            return new(ConflictStatus, proposal.PlanId, null,
                ReplacementConfirmationRequiredCode);

        var currentProfile = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new { profile.ConcurrencyVersion, profile.AvailableWorkoutWeekdaysJson })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (currentProfile is null || currentProfile.ConcurrencyVersion != inputSnapshot.ProfileVersion)
        {
            proposal.Status = ExpiredStatus;
            proposal.DecidedAtUtc = DateTime.UtcNow;
            proposal.ConcurrencyVersion++;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new(StaleStatus, proposal.PlanId, null,
                WorkoutPreferencesChangedSinceProposalCode);
        }
        var draft = inputSnapshot.Request.DraftPreferences;
        if (draft is null || !WorkoutPreferencesRule.IsValid(draft.ActivityTypes, draft.AvailableWeekdays,
                draft.ActivitySessionDurationsMinutes, draft.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(draft.EquipmentLevel))
            return new(InvalidProposalStatus, null, null,
                DraftPreferencesInvalidCode);
        var availableWeekdays = draft.AvailableWeekdays.Select(value => (DayOfWeek)value).ToHashSet();

        if (!TryGetMixedSchedule(proposalSnapshot, availableWeekdays, out var scheduleSessions) ||
            !TryGetStrengthSessions(proposalSnapshot.Sessions, scheduleSessions, availableWeekdays,
                out var strengthSessions, out var assignments))
        {
            var reasonCode = ContainsUnrepresentableOtherActivity(proposalSnapshot)
                ? UnsupportedOtherActivityCode
                : MixedScheduleInvalidCode;
            return new(LimitedStatus, null, null,
                reasonCode);
        }

        var exerciseIds = strengthSessions.Values.SelectMany(session => session.Slots)
            .Where(slot => slot.ExerciseId.HasValue).Select(slot => slot.ExerciseId!.Value).Distinct().ToArray();
        var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .Include(row => row.MuscleContributions).ThenInclude(row => row.MuscleGroup)
            .Where(row => exerciseIds.Contains(row.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var currentGroups = definitions.Where(row => row.MuscleContributions.Any(item => item.Fraction > 0) &&
                row.MuscleContributions.Where(item => item.Fraction > 0).All(item => !string.IsNullOrWhiteSpace(item.MuscleGroup?.Name)))
            .ToDictionary(row => row.Id, row => row.MuscleContributions.Where(item => item.Fraction > 0)
                .Select(item => item.MuscleGroup!.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
        if (exerciseIds.Length > 0 && (proposalSnapshot.SourceMuscleGroupsByExerciseId is null || exerciseIds.Any(id =>
                !currentGroups.TryGetValue(id, out var current) ||
                !proposalSnapshot.SourceMuscleGroupsByExerciseId.TryGetValue(id, out var original) ||
                !current.SetEquals(original))))
            return new(StaleStatus, proposal.PlanId, null,
                ExerciseMuscleMetadataChangedCode);
        var validationSessions = scheduleSessions.Select(session =>
        {
            IReadOnlySet<string>? groups = null;
            if (IsStrengthActivity((WorkoutActivityType)session.ActivityTypeId) &&
                strengthSessions.TryGetValue(session.Weekday, out var strength))
                groups = strength.Slots.Where(slot => slot.ExerciseId.HasValue)
                    .SelectMany(slot => currentGroups[slot.ExerciseId!.Value])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return ActivityOccurrencePrescription.ValidationSession(session,
                proposalSnapshot.MixedActivitySchedule!.Activities.Single(decision =>
                    decision.ActivityTypeId == session.ActivityTypeId), groups);
        }).ToArray();
        if (WorkoutScheduleValidationRule.Validate(validationSessions).Count != 0)
            return new(LimitedStatus, null, null,
                ProposalScheduleSafetyInvalidCode);

        var nowUtc = DateTime.UtcNow;
        var now = new DateTimeOffset(nowUtc);
        var today = WorkoutAccountCalendarRule.LocalDate(now, zone);
        if (scheduleSessions.Any(session => session.ActivityTypeId == (int)WorkoutActivityType.Hiit))
        {
            var savedAnswers = string.IsNullOrWhiteSpace(planningState.AnswersJson)
                ? null : Deserialize<SavedActivityAnswersDto>(planningState.AnswersJson);
            if (savedAnswers?.HiitCapture is not { } capture ||
                !HiitEvidenceCaptureRule.IsCaptureCurrent(capture.LocalDate, capture.TimeZoneVersion,
                    today, planningState.TimeZoneVersion))
                return new(StaleStatus, proposal.PlanId, null, PlanningInputsChangedSinceProposalCode);
        }
        if (inputSnapshot.Request.LocalDate != today)
            return new(StaleStatus, proposal.PlanId, null, PlanningDateChangedCode);
        // Exercise logs can reach 100% before FinishSession, and legacy session
        // completion timestamps can be client supplied. Credits are committed with
        // verified completion and preserve the calendar date earned at that time.
        var completedToday = await dbContext.WorkoutCompletionDayCredits.AsNoTracking()
            .AnyAsync(row => row.UserId == userId && row.LocalDate == today, cancellationToken)
            .ConfigureAwait(false);
        // Manual completions affect the user's next eligible workout date without awarding credit.
        var manualCompletedToday = await dbContext.WorkoutLogs.AsNoTracking().AnyAsync(log =>
            log.UserId == userId && log.PlannedSessionId == null && log.Date == today &&
            log.CompletionPercent >= 100 && dbContext.WorkoutDays.Any(day => day.UserId == userId &&
                day.Id == log.WorkoutDayId && day.WorkoutType != nameof(WorkoutType.Rest)), cancellationToken)
            .ConfigureAwait(false);
        manualCompletedToday |= await dbContext.WorkoutSessionLogs.AsNoTracking().AnyAsync(session =>
            session.UserId == userId && !session.IsDeleted && session.PlannedWorkoutDayId == null &&
            session.UnplannedActivityType != null && session.UnplannedCompletionLocalDate == today &&
            session.UnplannedCompletionTimeZoneId != null && session.UnplannedCompletionTimeZoneVersion != null &&
            session.CompletedAt != null, cancellationToken).ConfigureAwait(false);
        int? sameDayStartOrder = null;
        var firstDate = WorkoutPlanStartDateRule.FindNextEligibleDate(now, zone, availableWeekdays,
            completedToday || manualCompletedToday);
        if (origin == AcceptanceOrigin.Automatic)
        {
            if (activePlanId is null || inputSnapshot.AutomaticStartDate is null ||
                await WorkoutSessionAccountLock.HasActivePreviewAsync(dbContext, userId, nowUtc, cancellationToken).ConfigureAwait(false))
                return new(StaleStatus, proposal.PlanId, null, PlanningInputsChangedSinceProposalCode);
            var activeSessions = await InspectPromotionActiveSessionsAsync(userId, activePlanId.Value, cancellationToken).ConfigureAwait(false);
            if (!activeSessions.Proven || (activeSessions.Any && (planningState.SafetyRegenerationPending || !planningState.PromotionPending)))
                return new(StaleStatus, proposal.PlanId, null, PlanningInputsChangedSinceProposalCode);
            if (planningState.PromotionPending && !planningState.SafetyRegenerationPending &&
                (completedToday || activeSessions.Any) && !manualCompletedToday)
                sameDayStartOrder = await FindProvenSameDayPromotionOrderAsync(userId, activePlanId.Value,
                    today, scheduleSessions, cancellationToken).ConfigureAwait(false);
            var earliestScheduled = await FindNextScheduledDateAsync(userId, activePlanId.Value,
                today, ((completedToday || activeSessions.Any) && sameDayStartOrder is null) || manualCompletedToday, cancellationToken).ConfigureAwait(false);
            var expectedStart = planningState.SafetyRegenerationPending ? firstDate : earliestScheduled ?? firstDate;
            if (expectedStart is null || (manualCompletedToday && expectedStart == today) ||
                inputSnapshot.AutomaticStartDate != expectedStart ||
                !availableWeekdays.Contains(expectedStart.Value.DayOfWeek))
                return new(StaleStatus, proposal.PlanId, null, PlanningDateChangedCode);
            firstDate = expectedStart;
        }
        if (firstDate is null)
            return new(LimitedStatus, null, null, NoEligibleWorkoutDayCode);
        if (origin == AcceptanceOrigin.User && draft is not null)
        {
            var profile = await dbContext.UserProfiles.SingleAsync(
                item => item.UserId == userId, cancellationToken).ConfigureAwait(false);
            dbContext.UserProfileSnapshots.Add(new UserProfileSnapshotRecord
            {
                Id = Guid.CreateVersion7(), UserId = userId, SnapshotReason = "WorkoutPreferencesApply",
                ProfileJson = JsonSerializer.Serialize(profile), CreatedAt = nowUtc
            });
            profile.PreferredWorkoutActivityTypes = string.Join(',', draft.ActivityTypes
                .Select(value => ((WorkoutActivityType)value).ToString()).OrderBy(value => value, StringComparer.Ordinal));
            profile.AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(draft.AvailableWeekdays
                .Select(value => ((DayOfWeek)value).ToString()).OrderBy(value => value, StringComparer.Ordinal));
            profile.ActivitySessionDurationsJson = JsonSerializer.Serialize(draft.ActivitySessionDurationsMinutes
                .ToDictionary(pair => ((WorkoutActivityType)pair.Key).ToString(), pair => pair.Value, StringComparer.Ordinal));
            profile.RequestedActivitySessionsPerWeekJson = JsonSerializer.Serialize((draft.RequestedSessionsPerWeek ??
                new Dictionary<int, int>()).ToDictionary(pair => ((WorkoutActivityType)pair.Key).ToString(),
                    pair => pair.Value, StringComparer.Ordinal));
            profile.ExcludedStrengthExerciseIdsJson = JsonSerializer.Serialize((draft.ExcludedExerciseIds ??
                new HashSet<int>()).OrderBy(value => value).ToArray());
            if (draft.EquipmentLevel is not null) profile.EquipmentLevel = draft.EquipmentLevel;
            profile.UpdatedAt = nowUtc;
        }
        var revisionId = Guid.CreateVersion7();
        var plan = new WorkoutPlanRecord
        {
            Id = proposal.PlanId,
            UserId = userId,
            Name = MixedActivityPlanName,
            IsActive = true,
            CreatedAt = nowUtc
        };
        if (activePlanId is { } priorPlanId)
        {
            var previous = await dbContext.WorkoutPlans.SingleAsync(
                item => item.UserId == userId && item.Id == priorPlanId, cancellationToken)
                .ConfigureAwait(false);
            previous.IsActive = false;
        }
        dbContext.WorkoutPlans.Add(plan);

        var sessionsByWeekday = scheduleSessions
            .GroupBy(session => session.Weekday)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(session => session.DayOrder).ThenBy(session => session.ActivityTypeId).ToArray());
        var weekStart = MondayOf(firstDate.Value);
        var planDays = new List<WorkoutDayRecord>(OverviewScheduleWeeks *
            (DaysPerWeek + scheduleSessions.Length));
        var exercises = new List<WorkoutDayExerciseRecord>();
        for (var weekIndex = 0; weekIndex < OverviewScheduleWeeks; weekIndex++)
        for (var dayOffset = 0; dayOffset < DaysPerWeek; dayOffset++)
        {
            var date = weekStart.AddDays(weekIndex * DaysPerWeek + dayOffset);
            if (date < firstDate.Value) continue;
            var weekday = date.DayOfWeek;
            if (!sessionsByWeekday.TryGetValue(weekday, out var sessionsForDay))
            {
                planDays.Add(new WorkoutDayRecord
                {
                    Id = Guid.CreateVersion7(), UserId = userId, WorkoutPlanId = plan.Id,
                    DayOfWeek = (int)weekday, WorkoutType = nameof(WorkoutType.Rest),
                    Name = RestDayName, WeekIndex = weekIndex, Date = date
                });
                continue;
            }

            foreach (var scheduledSession in sessionsForDay)
            {
                if (date == today && sameDayStartOrder is { } startOrder &&
                    scheduledSession.DayOrder < startOrder) continue;
                var activity = (WorkoutActivityType)scheduledSession.ActivityTypeId;
                var dayId = Guid.CreateVersion7();
                planDays.Add(new WorkoutDayRecord
                {
                    Id = dayId, UserId = userId, WorkoutPlanId = plan.Id,
                    DayOfWeek = (int)weekday, WorkoutType = ToWorkoutType(activity),
                    ActivityTypeId = scheduledSession.ActivityTypeId, DayOrder = scheduledSession.DayOrder,
                    Name = ActivityName(activity), WeekIndex = weekIndex, Date = date,
                    PlannedMainDurationMinutes = scheduledSession.DurationMinutes,
                    PlannedActivityPrescriptionJson = ActivityOccurrencePrescription.Serialize(
                        scheduledSession.OccurrencePrescription ?? proposalSnapshot.MixedActivitySchedule!.Activities
                            .Single(decision => decision.ActivityTypeId == scheduledSession.ActivityTypeId).OriginalPrescription)
                });

                if (!IsStrengthActivity(activity) ||
                    !strengthSessions.TryGetValue(weekday, out var strengthSession)) continue;

                foreach (var slot in strengthSession.Slots.Where(slot => slot.ExerciseId.HasValue &&
                    slot.Sets is >= MinimumExerciseSetCount &&
                    slot.MinimumRepetitions is >= MinimumRepetitionCount))
                    exercises.Add(new WorkoutDayExerciseRecord
                    {
                        Id = Guid.CreateVersion7(), UserId = userId, WorkoutDayId = dayId,
                        ExerciseId = slot.ExerciseId!.Value,
                        PlanningTemplateId = strengthSession.TemplateId,
                        PlanningTemplateVersion = strengthSession.TemplateVersion,
                        PlanningSlotId = slot.SlotId,
                        PlanningSessionIndex = strengthSession.SessionIndex,
                        Sets = slot.Sets!.Value, Reps = slot.MinimumRepetitions!.Value,
                        TargetRepetitionsInReserve = slot.RepetitionsInReserve,
                        IsOptional = !slot.IsRequired
                    });
            }
        }

        await PostgresCanonicalOccurrenceAdjustment.ApplyAsync(dbContext, userId, activePlanId,
            today, firstDate.Value, zone, proposalSnapshot.Sessions, planDays, exercises,
            cancellationToken).ConfigureAwait(false);
        dbContext.WorkoutDays.AddRange(planDays);
        dbContext.WorkoutDayExercises.AddRange(exercises);
        dbContext.WorkoutPlanRevisionsV02.Add(new WorkoutPlanRevisionV02Record
        {
            Id = revisionId,
            UserId = userId,
            PlanId = plan.Id,
            RevisionNumber = InitialRevisionNumber,
            SourceProposalId = proposal.Id,
            RulesetVersion = proposal.RulesetVersion,
            TrainingPolicyVersion = proposal.TrainingPolicyVersion,
            PlanJson = JsonSerializer.Serialize(new AcceptedPlanSnapshot(
                proposalSnapshot, assignments, scheduleSessions, OverviewScheduleWeeks), JsonOptions),
            InputSnapshotJson = proposal.InputSnapshotJson,
            AcceptedAtUtc = nowUtc
        });
        proposal.Status = AcceptedStatus;
        proposal.DecidedAtUtc = nowUtc;
        proposal.AcceptedRevisionId = revisionId;
        proposal.ConcurrencyVersion++;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AcceptStrengthPlanProposalResultV020(AcceptedStatus, plan.Id, revisionId, null);
    }

    internal async Task<(bool Any, bool Proven)> InspectPromotionActiveSessionsAsync(Guid userId, Guid activePlanId,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking().Where(session => session.UserId == userId &&
            !session.IsDeleted && session.CompletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (sessions.Count == 0) return (false, true);
        if (sessions.Count != 1 || sessions[0].PlannedWorkoutDayId is not { } dayId) return (true, false);
        var day = await dbContext.WorkoutDays.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId &&
            row.Id == dayId && row.WorkoutPlanId == activePlanId, cancellationToken).ConfigureAwait(false);
        var snapshot = PlannedPrescriptionSnapshot.Deserialize(sessions[0].PlannedPrescriptionSnapshotJson);
        if (day is null || day.IsRemovedBySafety || day.IsRemovedByReadiness || day.IsRemovedByUserAdjustment ||
            snapshot is null || snapshot.DayId != dayId || snapshot.Exercises is null ||
            snapshot.WorkoutType != day.WorkoutType || snapshot.ActivityTypeId != day.ActivityTypeId ||
            snapshot.ActivityTypeId is not { } activity || !Enum.IsDefined(typeof(WorkoutActivityType), activity) ||
            !IsPersistableActivity((WorkoutActivityType)activity) ||
            (IsStrengthActivity((WorkoutActivityType)activity) && snapshot.Exercises.Count == 0) ||
            snapshot.Exercises.Any(exercise => exercise is null || exercise.Id == Guid.Empty || exercise.ExerciseId <= 0 ||
                exercise.Sets <= 0 || exercise.Reps < 0 ||
                (exercise.Reps == 0 && (IsStrengthActivity((WorkoutActivityType)activity) || exercise.DurationSeconds is null)) ||
                exercise.OptionalSets < 0 ||
                exercise.OptionalSets >= exercise.Sets || exercise.DurationSeconds is <= 0) ||
            snapshot.Exercises.Select(exercise => exercise.Id).Distinct().Count() != snapshot.Exercises.Count)
            return (true, false);
        var ids = snapshot.Exercises.Select(exercise => exercise.Id).ToArray();
        var originalRows = await dbContext.WorkoutDayExercises.AsNoTracking().Where(exercise =>
            exercise.UserId == userId && exercise.WorkoutDayId == dayId && ids.Contains(exercise.Id))
            .Select(exercise => new { exercise.Id, exercise.ExerciseId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (true, originalRows.Count == ids.Length && snapshot.Exercises.All(exercise =>
            originalRows.Any(row => row.Id == exercise.Id && row.ExerciseId == exercise.ExerciseId)));
    }

    internal async Task<int?> FindProvenSameDayPromotionOrderAsync(Guid userId, Guid activePlanId,
        DateOnly today, IReadOnlyCollection<MixedActivityScheduleSessionV020> proposed,
        CancellationToken cancellationToken)
    {
        var days = await dbContext.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId &&
            day.WorkoutPlanId == activePlanId && day.Date == today && day.WorkoutType != nameof(WorkoutType.Rest))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (days.Any(day => day.IsRemovedBySafety || day.IsRemovedByReadiness || day.IsRemovedByUserAdjustment))
            return null;
        var dayIds = days.Select(day => day.Id).ToArray();
        var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking().Where(session => session.UserId == userId &&
            !session.IsDeleted && session.PlannedWorkoutDayId != null &&
            (session.CompletedAt == null || dayIds.Contains(session.PlannedWorkoutDayId.Value)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (sessions.Any(session => session.CompletedAt == null && !dayIds.Contains(session.PlannedWorkoutDayId!.Value)))
            return null;
        var retiredTodayIds = await dbContext.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId &&
            day.WorkoutPlanId != activePlanId && day.Date == today).Select(day => day.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (await dbContext.WorkoutSessionLogs.AsNoTracking().AnyAsync(session => session.UserId == userId &&
                !session.IsDeleted && session.CompletedAt != null && session.PlannedWorkoutDayId != null &&
                retiredTodayIds.Contains(session.PlannedWorkoutDayId.Value), cancellationToken).ConfigureAwait(false))
            return null;
        var credits = await dbContext.WorkoutCompletionDayCredits.AsNoTracking()
            .Where(credit => credit.UserId == userId && credit.LocalDate == today)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var creditLogIds = credits.Where(credit => credit.SourceWorkoutLogId != null)
            .Select(credit => credit.SourceWorkoutLogId!.Value).ToArray();
        var creditLogs = await dbContext.WorkoutLogs.AsNoTracking().Where(log => log.UserId == userId &&
            creditLogIds.Contains(log.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (credits.Any(credit =>
            (credit.SourceSessionId is null && credit.SourceWorkoutLogId is null) ||
            (credit.SourceSessionId is { } sessionId && !sessions.Any(session => session.Id == sessionId &&
                session.CompletedAt != null && dayIds.Contains(session.PlannedWorkoutDayId!.Value))) ||
            (credit.SourceWorkoutLogId is { } logId && !creditLogs.Any(log => log.Id == logId &&
                dayIds.Contains(log.WorkoutDayId) && sessions.Any(session => session.CompletedAt != null &&
                    session.PlannedWorkoutDayId == log.WorkoutDayId &&
                    (credit.SourceSessionId == null || session.Id == credit.SourceSessionId))))))
            return null;
        var existing = days.Select(day => new PromotionExistingWorkout(
            day.ActivityTypeId is { } activity && Enum.IsDefined(typeof(WorkoutActivityType), activity)
                ? (WorkoutActivityType?)activity : null, day.DayOrder,
            sessions.Any(session => session.PlannedWorkoutDayId == day.Id && session.CompletedAt != null),
            sessions.Any(session => session.PlannedWorkoutDayId == day.Id && session.CompletedAt == null), true)).ToArray();
        var next = proposed.Where(session => session.Weekday == today.DayOfWeek)
            .Select(session => new PromotionProposedWorkout((WorkoutActivityType)session.ActivityTypeId, session.DayOrder))
            .ToArray();
        return PromotionSameDayBoundaryRule.FindSuccessorOrder(existing, next);
    }

    internal async Task<DateOnly?> FindNextScheduledDateAsync(Guid userId, Guid activePlanId,
        DateOnly today, bool completedToday, CancellationToken cancellationToken)
    {
        return await dbContext.WorkoutDays.AsNoTracking()
                .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlanId &&
                    day.Date != null && day.Date >= today && day.WorkoutType != nameof(WorkoutType.Rest) &&
                    !day.IsRemovedByReadiness && !day.IsRemovedByUserAdjustment && !day.IsRemovedBySafety &&
                    !(completedToday && day.Date == today) &&
                    !dbContext.WorkoutSessionLogs.Any(session => session.UserId == userId &&
                        session.PlannedWorkoutDayId == day.Id && session.CompletedAt != null) &&
                    !dbContext.WorkoutCompletionDayCredits.Any(credit => credit.UserId == userId &&
                        credit.SourceWorkoutLogId != null && dbContext.WorkoutLogs.Any(log =>
                            log.UserId == userId && log.Id == credit.SourceWorkoutLogId && log.WorkoutDayId == day.Id)))
                .OrderBy(day => day.Date).Select(day => day.Date).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - MondayWeekOrderOffset + DaysPerWeek) % DaysPerWeek;
        return date.AddDays(-offset);
    }

    private static bool TryGetMixedSchedule(ProposalSnapshot proposal,
        IReadOnlySet<DayOfWeek> availableWeekdays,
        out MixedActivityScheduleSessionV020[] scheduleSessions)
    {
        scheduleSessions = [];
        var schedule = proposal.MixedActivitySchedule;
        if (schedule is null || schedule.Sessions is not { Count: > 0 } || schedule.Activities is null ||
            schedule.Sessions.Count > availableWeekdays.Count *
            MixedActivityWeekScheduleRule.MaximumSessionsPerDay)
            return false;

        scheduleSessions = schedule.Sessions.ToArray();
        if (scheduleSessions.Any(session => session is null)) return false;
        // Older stored proposals contain no ordering provenance. New proposals must
        // provide a complete, contiguous explicit order for each scheduled day.
        if (scheduleSessions.Any(session => session?.DayOrder is not null) &&
            scheduleSessions.GroupBy(session => session.Weekday).Any(day =>
                !day.Select(session => session.DayOrder).OrderBy(order => order)
                    .SequenceEqual(Enumerable.Range(WorkoutSessionOrderingRule.FirstSessionOrder, day.Count())
                        .Select(order => (int?)order))))
            return false;
        var decisions = schedule.Activities;
        if (decisions.Any(decision => decision is null ||
                                      !Enum.IsDefined((WorkoutActivityType)decision.ActivityTypeId) ||
                                      decision.ScheduledSessionsPerWeek < 0 ||
                                      !IsValidDisposition(decision.Disposition)) ||
            decisions.Select(decision => decision.ActivityTypeId).Distinct().Count() != decisions.Count)
            return false;

        var decisionByActivity = decisions.ToDictionary(decision => decision.ActivityTypeId);
        if (scheduleSessions.Any(session => session is null ||
                !IsPersistableActivity((WorkoutActivityType)session.ActivityTypeId) ||
                !Enum.IsDefined(session.Weekday) || !availableWeekdays.Contains(session.Weekday) ||
                session.DurationMinutes <= MinimumPlannedActivityDurationMinutes ||
                !decisionByActivity.TryGetValue(session.ActivityTypeId, out var decision) ||
                !IsScheduledDisposition(decision.Disposition) ||
                !ActivityOccurrencePrescription.IsValid(session, decision)))
            return false;

        if (WorkoutScheduleValidationRule.Validate(scheduleSessions.Select(session =>
                ActivityOccurrencePrescription.ValidationSession(session,
                    decisionByActivity[session.ActivityTypeId])).ToArray(),
                requireStrengthMuscles: false).Count != 0)
            return false;

        var scheduledCountByActivity = scheduleSessions
            .GroupBy(session => session.ActivityTypeId)
            .ToDictionary(group => group.Key, group => group.Count());
        return decisions.All(decision =>
            decision.ScheduledSessionsPerWeek ==
            (scheduledCountByActivity.TryGetValue(decision.ActivityTypeId, out var count) ? count : 0) &&
            ((decision.ScheduledSessionsPerWeek > 0) == IsScheduledDisposition(decision.Disposition)));
    }

    private static bool ContainsUnrepresentableOtherActivity(ProposalSnapshot proposal) =>
        proposal.MixedActivitySchedule?.Sessions?.Any(session => session is not null &&
            session.ActivityTypeId == (int)WorkoutActivityType.Other) == true;

    private static bool TryGetStrengthSessions(IReadOnlyList<StrengthPlanPreviewSessionV020> proposalSessions,
        IReadOnlyList<MixedActivityScheduleSessionV020> scheduleSessions,
        IReadOnlySet<DayOfWeek> availableWeekdays,
        out IReadOnlyDictionary<DayOfWeek, StrengthPlanPreviewSessionV020> strengthSessions,
        out IReadOnlyList<StrengthWeekdayAssignment> assignments)
    {
        strengthSessions = new Dictionary<DayOfWeek, StrengthPlanPreviewSessionV020>();
        assignments = [];
        if (proposalSessions.Any(session => session is null))
            return false;

        var orderedSessions = proposalSessions.OrderBy(session => session.SessionIndex).ToArray();
        var plannedWeekdays = orderedSessions.Select(session => session.PlannedWeekday).ToArray();
        if (orderedSessions.Any(session => session.SessionIndex < MinimumSessionIndex || session.Slots is null) ||
            orderedSessions.Select(session => session.SessionIndex).Distinct().Count() != orderedSessions.Length ||
            plannedWeekdays.Any(weekday => weekday is null || !Enum.IsDefined(weekday.Value) ||
                                           !availableWeekdays.Contains(weekday.Value)) ||
            plannedWeekdays.Distinct().Count() != orderedSessions.Length)
            return false;

        var strengthSchedule = scheduleSessions
            .Where(session => IsStrengthActivity((WorkoutActivityType)session.ActivityTypeId))
            .ToArray();
        if (strengthSchedule.Length != orderedSessions.Length ||
            strengthSchedule.Any(session => !plannedWeekdays.Contains(session.Weekday)))
            return false;

        strengthSessions = orderedSessions.ToDictionary(session => session.PlannedWeekday!.Value);
        assignments = orderedSessions.Select(session =>
            new StrengthWeekdayAssignment(session.SessionIndex, session.PlannedWeekday!.Value)).ToArray();
        return true;
    }

    private static bool IsValidDisposition(string disposition) =>
        Enum.TryParse<MixedActivityScheduleDisposition>(disposition, true, out var parsed) &&
        Enum.IsDefined(parsed);

    private static bool IsScheduledDisposition(string disposition) =>
        Enum.TryParse<MixedActivityScheduleDisposition>(disposition, true, out var parsed) &&
        parsed is MixedActivityScheduleDisposition.Scheduled or MixedActivityScheduleDisposition.PartiallyScheduled;

    private static bool IsStrengthActivity(WorkoutActivityType activity) => activity is
        WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.GeneralStrength;

    private static bool IsPersistableActivity(WorkoutActivityType activity) =>
        Enum.IsDefined(activity) && activity is not WorkoutActivityType.RestDay and not WorkoutActivityType.Other;

    private static string ToWorkoutType(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.GeneralStrength or WorkoutActivityType.RockClimbing => nameof(WorkoutType.Strength),
        WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery =>
            nameof(WorkoutType.Recovery),
        WorkoutActivityType.Hiit or WorkoutActivityType.Cycling or WorkoutActivityType.Running or
            WorkoutActivityType.Swimming or WorkoutActivityType.Walking or WorkoutActivityType.Rowing or
            WorkoutActivityType.Elliptical => nameof(WorkoutType.Cardio),
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unsupported scheduled activity.")
    };

    private static string ActivityName(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength => "High volume strength",
        WorkoutActivityType.StrengthHighIntensity => "Strength high intensity",
        WorkoutActivityType.GeneralStrength => "General strength training",
        WorkoutActivityType.RockClimbing => "Rock climbing",
        WorkoutActivityType.Yoga => "Yoga",
        WorkoutActivityType.Hiit => "HIIT",
        WorkoutActivityType.Cycling => "Cycling",
        WorkoutActivityType.Running => "Running",
        WorkoutActivityType.Swimming => "Swimming",
        WorkoutActivityType.Walking => "Walking",
        WorkoutActivityType.Rowing => "Rowing",
        WorkoutActivityType.Elliptical => "Elliptical",
        WorkoutActivityType.Mobility => "Mobility",
        WorkoutActivityType.GentleRecovery => "Gentle recovery",
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unsupported scheduled activity.")
    };

    private sealed record ProposalInputSnapshot(
        StrengthPlanPreviewRequestV020 Request,
        int ProfileVersion,
        Guid? BaselineActivePlanId,
        Guid? BaselineRevisionId,
        bool AutomaticOrigin = false,
        DateOnly? AutomaticStartDate = null);

    private sealed record ProposalSnapshot(
        string PolicyId,
        string PolicyVersion,
        string RulesetVersion,
        string Status,
        IReadOnlyList<StrengthPlanPreviewSessionV020> Sessions,
        MixedActivityScheduleV020? MixedActivitySchedule = null,
        string? SourceHistoryFingerprint = null,
        string? SourceCatalogFingerprint = null,
        string? SourceInjuryFingerprint = null,
        long SourceTimeZoneVersion = 0,
        long SourceExperienceVersion = 0,
        long SourceAnswerVersion = 0,
        IReadOnlyDictionary<int, string[]>? SourceMuscleGroupsByExerciseId = null,
        bool RequiresReducedVolumeAcknowledgment = false,
        string? InjuryProvenanceFingerprint = null);

    private sealed record AcceptedPlanSnapshot(
        ProposalSnapshot Proposal,
        IReadOnlyList<StrengthWeekdayAssignment> WeekdayAssignments,
        IReadOnlyList<MixedActivityScheduleSessionV020> ScheduleSessions,
        int RepeatedWeeks);
}
