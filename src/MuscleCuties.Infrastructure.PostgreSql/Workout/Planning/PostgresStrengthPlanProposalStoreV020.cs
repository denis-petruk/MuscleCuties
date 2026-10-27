using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Stores immutable v0.2 strength proposals under the authenticated tenant context.</summary>
public sealed class PostgresStrengthPlanProposalStoreV020(PostgresDbContext dbContext)
    : IStrengthPlanProposalStoreV020
{
    private const string ProposedStatus = "Proposed";
    private const string ExpiredStatus = "Expired";
    private const string FeasibleStatus = "Feasible";
    private const string StaleStatus = "Stale";
    private const string ActiveStatus = "Active";
    private const string ClosedStatus = "Closed";
    private const int InitialAnswerVersion = 0;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StrengthPlanProposalCreatedV020> SaveAsync(Guid userId, StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewResponseV020 preview, CancellationToken cancellationToken)
    {
        ValidateProposalInput(userId, request, preview);

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var result = await SaveInCurrentTransactionAsync(userId, request, preview, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);
    }

    // Caller owns the tenant transaction and account/catalog locks and commits the result.
    internal async Task<StrengthPlanProposalCreatedV020> SaveInCurrentTransactionAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, StrengthPlanPreviewResponseV020 preview,
        CancellationToken cancellationToken, bool automaticOrigin = false, DateOnly? automaticStartDate = null)
    {
        ValidateProposalInput(userId, request, preview);
        if (!automaticOrigin && automaticStartDate is not null)
            throw new ArgumentException("A user proposal cannot specify an automatic start date.");

        var now = DateTime.UtcNow;
        var prior = await dbContext.WorkoutPlanProposalsV02
            .Where(item => item.UserId == userId && item.LeaseId != null)
            .OrderByDescending(item => item.LeaseFence).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (prior.Any(item => item.Status == "Proposed" && item.LeaseExpiresAtUtc > now))
            throw new WorkoutPreviewAlreadyOpenException();
        foreach (var expired in prior.Where(item => item.Status == "Proposed"))
        {
            expired.Status = "Expired";
            expired.DecidedAtUtc = now;
            expired.ConcurrencyVersion++;
        }
        var fence = PlanningLifecycleRule.NextFence(prior.FirstOrDefault()?.LeaseFence ?? 0)
            ?? throw new InvalidOperationException("Workout planning fence exhausted.");

        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId && plan.IsActive)
            .OrderByDescending(plan => plan.CreatedAt)
            .Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var profileVersion = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (int?)profile.ConcurrencyVersion)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profileVersion is null)
            throw new InvalidOperationException("A planning profile is required to persist a proposal.");
        if (preview.SourceProfileVersion != profileVersion)
            throw new WorkoutPlanningInputsChangedException();
        if (string.IsNullOrWhiteSpace(preview.SourceCatalogFingerprint) ||
            preview.SourceCatalogFingerprint != await PlanningCatalogFingerprint.LoadAsync(dbContext, userId, cancellationToken).ConfigureAwait(false))
            throw new WorkoutPlanningInputsChangedException();
        var injuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (preview.SourceInjuryFingerprint != PlanningInputFingerprint.Injuries(injuries))
            throw new WorkoutPlanningInputsChangedException();
        var logs = await dbContext.WorkoutExerciseLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.CompletedSets > 0 && row.CompletedReps > 0)
            .Select(row => new { row.ExerciseId, row.WorkoutLogId, row.WeightKg })
            .OrderBy(row => row.WorkoutLogId).ThenBy(row => row.ExerciseId).ThenBy(row => row.WeightKg)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (preview.SourceHistoryFingerprint != PlanningInputFingerprint.Hash(logs))
            throw new WorkoutPlanningInputsChangedException();
        var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (preview.SourceTimeZoneVersion != (planningState?.TimeZoneVersion ?? WorkoutAccountCalendarRule.InitialTimeZoneVersion) ||
            preview.SourceExperienceVersion != (planningState?.ExperienceVersion ?? 0) ||
            preview.SourceAnswerVersion != (planningState?.AnswerVersion ?? 0))
            throw new WorkoutPlanningInputsChangedException();
        var activeRevisionId = activePlanId is { } planId
            ? await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                .Where(revision => revision.UserId == userId && revision.PlanId == planId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .Select(revision => (Guid?)revision.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;
        if (preview.SourceActivePlanId != activePlanId || preview.SourceActiveRevisionId != activeRevisionId)
            throw new WorkoutPlanningInputsChangedException();

        var snapshotJson = JsonSerializer.Serialize(
            new ProposalInputSnapshot(request, profileVersion.Value, activePlanId, activeRevisionId,
                automaticOrigin, automaticStartDate), JsonOptions);
        var previewJson = JsonSerializer.Serialize(preview, JsonOptions);
        var record = new WorkoutPlanProposalV02Record
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PlanId = Guid.CreateVersion7(),
            ExpectedRevisionId = Guid.Empty,
            RulesetVersion = preview.RulesetVersion,
            TrainingPolicyVersion = preview.PolicyVersion,
            Status = "Proposed",
            InputSnapshotJson = snapshotJson,
            ProposalJson = previewJson,
            ReasonsJson = JsonSerializer.Serialize(
                preview.LimitationCodes.Concat(preview.AdvisoryCodes).Distinct(StringComparer.Ordinal),
                JsonOptions),
            CreatedAtUtc = now,
            LeaseId = automaticOrigin ? null : Guid.CreateVersion7(),
            LeaseFence = fence,
            LeaseExpiresAtUtc = automaticOrigin ? null : now.Add(PlanningLifecycleRule.FiveMinuteLeaseDuration)
        };
        dbContext.WorkoutPlanProposalsV02.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new StrengthPlanProposalCreatedV020(record.Id, preview, true,
            automaticOrigin ? null : ToLease(record), record.ConcurrencyVersion, activePlanId is not null);
    }

    private static void ValidateProposalInput(Guid userId, StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewResponseV020 preview)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(preview);
        if (userId == Guid.Empty || request.DraftPreferences is null ||
            !string.Equals(preview.Status, "Feasible", StringComparison.Ordinal) ||
            preview.MixedActivitySchedule?.Sessions is not { Count: > 0 } ||
            preview.PolicyId != StrengthPlanPreviewRule.PolicyId ||
            preview.PolicyVersion != StrengthPlanPreviewRule.Version ||
            preview.RulesetVersion != StrengthPlanPreviewRule.RulesetVersion)
            throw new ArgumentException("Only a feasible proposal from the active training policy can be saved.");

    }

    public Task<PlanningLeaseResult> RenewAsync(Guid userId, PlanningLeaseRequest request, CancellationToken cancellationToken) =>
        UpdateLeaseAsync(userId, request, false, false, cancellationToken);

    public Task<PlanningLeaseResult> CloseAsync(Guid userId, PlanningLeaseRequest request, CancellationToken cancellationToken) =>
        UpdateLeaseAsync(userId, request, true, false, cancellationToken);

    public Task<PlanningLeaseResult> TakeoverAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken) =>
        UpdateLeaseAsync(userId, new PlanningLeaseRequest(proposalId, Guid.Empty, 0), false, true, cancellationToken);

    public async Task<ActivePlanningPreviewDto> GetActivePreviewAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            var proposal = await dbContext.WorkoutPlanProposalsV02.AsNoTracking()
                .Where(row => row.UserId == userId && row.Status == ProposedStatus && row.LeaseId != null && row.LeaseExpiresAtUtc > now)
                .OrderByDescending(row => row.LeaseFence).Select(row => new { row.Id, row.LeaseExpiresAtUtc })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return proposal is null ? new ActivePlanningPreviewDto(null, null) :
                new ActivePlanningPreviewDto(proposal.Id, new DateTimeOffset(DateTime.SpecifyKind(proposal.LeaseExpiresAtUtc!.Value, DateTimeKind.Utc)));
        }).ConfigureAwait(false);

    public async Task<SavedActivityAnswersDto> ReadAnswersAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var state = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            var answers = DeserializeAnswers(state?.AnswersJson);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return answers with { Version = state?.AnswerVersion ?? InitialAnswerVersion, SafetyPlanChanged = false };
        }).ConfigureAwait(false);

    public async Task<SavedActivityAnswersDto> SaveAnswersAsync(Guid userId, SaveActivityAnswersRequest request,
        CancellationToken cancellationToken) => await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
        await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
        var state = await dbContext.WorkoutAccountPlanningStates.SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Establish the account timezone first.");
        if (request.ActivityEligibilityEvidence is null)
            throw new WorkoutPlanningInputsChangedException();
        if (request.HiitUnderlyingCardioActivity is { } underlying &&
            !WorkoutPlanningConstants.IsSupportedHiitUnderlyingCardioActivity(underlying))
            throw new WorkoutPlanningInputsChangedException();
        var answers = new SavedActivityAnswersDto(state.AnswerVersion, request.ActivityEligibilityEvidence,
            request.HiitEligibilityEvidence) { HiitUnderlyingCardioActivity = request.HiitUnderlyingCardioActivity };
        var json = JsonSerializer.Serialize(answers, JsonOptions);
        var previous = DeserializeAnswers(state.AnswersJson);
        var previousComparable = JsonSerializer.Serialize(previous with
            { Version = state.AnswerVersion, SafetyPlanChanged = false }, JsonOptions);
        var safetyPlanChanged = false;
        if (state.AnswerVersion != request.ExpectedVersion)
        {
            if (request.ExpectedVersion < InitialAnswerVersion ||
                request.ExpectedVersion != state.AnswerVersion - 1 || json != previousComparable)
                throw new WorkoutPlanningInputsChangedException();
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return previous with { Version = state.AnswerVersion };
        }
        if (json != previousComparable)
        {
            if (answers.ActivityEligibilityEvidence.GroupBy(item => item.Activity).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Activity answers contain duplicate activities.");
            var priorByActivity = previous.ActivityEligibilityEvidence.ToDictionary(item => item.Activity);
            var newlyUnsafe = answers.ActivityEligibilityEvidence.Where(item =>
                Classify(item) == ActivitySafetyAnswerState.Unsafe &&
                Classify(priorByActivity.GetValueOrDefault(item.Activity)) != ActivitySafetyAnswerState.Unsafe).ToArray();
            var currentByActivity = answers.ActivityEligibilityEvidence.ToDictionary(item => item.Activity);
            var safetyNoLongerEstablished = previous.ActivityEligibilityEvidence.Where(item =>
                Classify(item) == ActivitySafetyAnswerState.Safe &&
                Classify(currentByActivity.GetValueOrDefault(item.Activity)) == ActivitySafetyAnswerState.Unknown).ToArray();
            var newlySafe = answers.ActivityEligibilityEvidence.Any(item =>
                Classify(item) == ActivitySafetyAnswerState.Safe &&
                Classify(priorByActivity.GetValueOrDefault(item.Activity)) != ActivitySafetyAnswerState.Safe);
            // A choice or evidence change does not by itself establish current eligibility.
            // Suppress until canonical regeneration has checked all restrictions and history.
            var hiitEvidenceChanged = JsonSerializer.Serialize(previous.HiitEligibilityEvidence, JsonOptions) !=
                JsonSerializer.Serialize(answers.HiitEligibilityEvidence, JsonOptions) ||
                previous.HiitUnderlyingCardioActivity != answers.HiitUnderlyingCardioActivity ||
                SerializeUnderlyingHistories(previous.ActivityEligibilityEvidence) !=
                SerializeUnderlyingHistories(answers.ActivityEligibilityEvidence);
            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking().Where(row => row.UserId == userId && row.IsActive)
                .Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if ((newlySafe || newlyUnsafe.Length > 0 || safetyNoLongerEstablished.Length > 0 || hiitEvidenceChanged) && activePlanId is not null)
                state.SafetyRegenerationPending = true;
            if (newlyUnsafe.Length == 0 && safetyNoLongerEstablished.Length == 0 && !newlySafe && !hiitEvidenceChanged)
                await WorkoutSessionAccountLock.RequireNoPreviewAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            else if ((newlyUnsafe.Length > 0 || safetyNoLongerEstablished.Length > 0 || hiitEvidenceChanged) && activePlanId is { } planId)
            {
                var unsafeActivityIds = newlyUnsafe.Concat(safetyNoLongerEstablished).Select(item =>
                    (int)WorkoutActivityContractMapper.ToCore(item.Activity)).Distinct().ToList();
                if (hiitEvidenceChanged) unsafeActivityIds.Add((int)WorkoutActivityType.Hiit);
                safetyPlanChanged = await SuppressUnsafeActivitiesAsync(userId, planId, unsafeActivityIds, cancellationToken)
                    .ConfigureAwait(false);
            }
            if (newlyUnsafe.Length > 0 || safetyNoLongerEstablished.Length > 0 || newlySafe || hiitEvidenceChanged)
            {
                var previews = await dbContext.WorkoutPlanProposalsV02.Where(item => item.UserId == userId && item.Status == ProposedStatus && item.LeaseId != null)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var item in previews)
                {
                    item.Status = ExpiredStatus; item.DecidedAtUtc = DateTime.UtcNow; item.ConcurrencyVersion++;
                }
            }
            state.AnswerVersion = checked(state.AnswerVersion + 1);
            state.AnswersJson = JsonSerializer.Serialize(answers with
                { Version = state.AnswerVersion, SafetyPlanChanged = safetyPlanChanged }, JsonOptions);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (state.SafetyRegenerationPending)
                await new PostgresWorkoutPlanReconciler(dbContext).ReconcileAsync(userId, cancellationToken)
                    .ConfigureAwait(false);

        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return answers with { Version = state.AnswerVersion, SafetyPlanChanged = safetyPlanChanged };
    }).ConfigureAwait(false);

    private static SavedActivityAnswersDto DeserializeAnswers(string? json) => string.IsNullOrWhiteSpace(json)
        ? new SavedActivityAnswersDto(InitialAnswerVersion, [], null)
        : JsonSerializer.Deserialize<SavedActivityAnswersDto>(json, JsonOptions)
          ?? throw new InvalidOperationException("Saved activity answers are invalid.");

    private static string SerializeUnderlyingHistories(IEnumerable<ActivityEligibilityInputV020> answers) =>
        JsonSerializer.Serialize(answers.Where(item => HiitEligibilityEvidenceRule.IsSupportedUnderlyingCardioActivity(
                WorkoutActivityContractMapper.ToCore(item.Activity)))
            .OrderBy(item => item.Activity).Select(item => new { item.Activity, item.ActivityHistory }), JsonOptions);

    private static ActivitySafetyAnswerState Classify(ActivityEligibilityInputV020? item) => item is null
        ? ActivitySafetyAnswerState.Unknown
        : ActivitySafetyAnswersRule.Classify(new ActivitySafetyAnswers(WorkoutActivityContractMapper.ToCore(item.Activity),
            item.HasSwimmingFacilityAccess, item.CanSwimIndependently, item.WantsContinuousSwimming,
            item.HasRopeClimbingCompetence, item.AppropriateClimbingInstructionAvailable, item.IsDemandingYogaContent));

    private async Task<bool> SuppressUnsafeActivitiesAsync(Guid userId, Guid planId,
        IReadOnlyList<int> activityIds, CancellationToken cancellationToken)
    {
        var state = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .SingleAsync(item => item.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (state.TimeZoneId is null) throw new InvalidOperationException("Establish the account timezone first.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("Establish the account timezone first."); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("Establish the account timezone first."); }
        var today = WorkoutAccountCalendarRule.LocalDate(DateTimeOffset.UtcNow, zone);
        var days = await dbContext.WorkoutDays.Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
                day.Date >= today && day.WorkoutType != nameof(WorkoutType.Rest) &&
                (day.ActivityTypeId == null || activityIds.Contains(day.ActivityTypeId.Value)) &&
                !dbContext.WorkoutCompletionDayCredits.Any(credit => credit.UserId == userId &&
                    credit.SourceWorkoutLogId != null && dbContext.WorkoutLogs.Any(log =>
                        log.UserId == userId && log.Id == credit.SourceWorkoutLogId && log.WorkoutDayId == day.Id)) &&
                !dbContext.WorkoutSessionLogs.Any(session => session.UserId == userId &&
                    session.PlannedWorkoutDayId == day.Id && (session.CompletedAt != null || !session.IsDeleted)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = days.Any(day => !day.IsRemovedBySafety);
        foreach (var day in days)
        {
            day.IsRemovedBySafety = true;
            day.SafetyActivityTypeId ??= day.ActivityTypeId ?? activityIds[0];
        }
        return changed;
    }

    private async Task<PlanningLeaseResult> UpdateLeaseAsync(Guid userId, PlanningLeaseRequest request, bool close,
        bool takeover, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var item = await dbContext.WorkoutPlanProposalsV02.SingleOrDefaultAsync(
                row => row.UserId == userId && row.Id == request.ProposalId, cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (item is null || item.Status != "Proposed" || item.LeaseId is null || item.LeaseExpiresAtUtc is null)
                return new PlanningLeaseResult("Stale", null);
            if (!takeover && (item.LeaseId != request.LeaseId || item.LeaseFence != request.Fence || item.LeaseExpiresAtUtc <= now))
                return new PlanningLeaseResult("Stale", null);
            if (close || (takeover && item.LeaseExpiresAtUtc <= now))
            {
                item.Status = "Expired";
                item.DecidedAtUtc = now;
                item.ConcurrencyVersion++;
            }
            else if (takeover)
            {
                item.LeaseId = Guid.CreateVersion7();
                item.LeaseFence = PlanningLifecycleRule.NextFence(item.LeaseFence)
                    ?? throw new InvalidOperationException("Workout planning fence exhausted.");
                item.LeaseExpiresAtUtc = now.Add(PlanningLifecycleRule.FiveMinuteLeaseDuration);
            }
            else item.LeaseExpiresAtUtc = now.Add(PlanningLifecycleRule.FiveMinuteLeaseDuration);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PlanningLeaseResult(item.Status == "Proposed" ? "Active" : "Closed",
                item.Status == "Proposed" ? ToLease(item) : null);
        }).ConfigureAwait(false);

    private static PlanningLeaseDto ToLease(WorkoutPlanProposalV02Record item) =>
        new(item.LeaseId!.Value, item.LeaseFence, new DateTimeOffset(DateTime.SpecifyKind(item.LeaseExpiresAtUtc!.Value, DateTimeKind.Utc)));

    private sealed record ProposalInputSnapshot(
        StrengthPlanPreviewRequestV020 Request,
        int ProfileVersion,
        Guid? BaselineActivePlanId,
        Guid? BaselineRevisionId,
        bool AutomaticOrigin = false,
        DateOnly? AutomaticStartDate = null);
}
