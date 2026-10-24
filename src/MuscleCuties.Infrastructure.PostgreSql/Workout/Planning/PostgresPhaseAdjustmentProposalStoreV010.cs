using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Persists a reviewable phase proposal and applies only the fresh, confirmed version.</summary>
public sealed class PostgresPhaseAdjustmentProposalStoreV010(
    PostgresDbContext dbContext,
    IActivePlanPhaseAdjustmentSourceV010 source,
    TimeProvider timeProvider) : IPhaseAdjustmentProposalStoreV010
{
    private const string ProposedStatus = "Proposed";
    private const string AcceptedStatus = "Accepted";
    private const string RejectedStatus = "Rejected";
    private const string ExpiredStatus = "Expired";
    private const string InvalidInputStatus = "InvalidInput";
    private const string ConflictStatus = "Conflict";
    private const string NotFoundStatus = "NotFound";
    private const string PhaseSnapshotStaleCode = "PhaseAdjustmentReviewChanged";
    private const string ProposalNotFoundCode = "PhaseAdjustmentProposalNotFound";
    private const string AlreadyDecidedCode = "PhaseAdjustmentProposalAlreadyDecided";
    private const string NoChangesCode = "PhaseAdjustmentNoChangesAvailable";
    private const int ConcurrencyVersionIncrement = 1;
    private const int MinimumRequiredSetCount = 1;
    private const int MinimumOptionalSetCount = 0;
    private const long NoPreviousRevision = 0L;
    private const long RevisionNumberIncrement = 1L;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> SaveAsync(Guid userId, PhaseAdjustmentProposalSnapshotV010 snapshot,
        PhaseAdjustmentPreview preview, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (userId == Guid.Empty || snapshot.PlanId == Guid.Empty ||
                snapshot.SourceRevisionId == Guid.Empty || string.IsNullOrWhiteSpace(snapshot.TimeZoneId) ||
                preview.Status !=
                PhaseAdjustmentPreviewStatus.ChangesOrChoicesAvailable)
                return Guid.Empty;

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(plan => plan.UserId == userId && plan.IsActive)
                .OrderByDescending(plan => plan.CreatedAt)
                .Select(plan => (Guid?)plan.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var activeRevisionId = activePlanId is { } planId
                ? await LatestRevisionIdAsync(userId, planId, cancellationToken).ConfigureAwait(false)
                : null;
            if (activePlanId != snapshot.PlanId || activeRevisionId != snapshot.SourceRevisionId)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Guid.Empty;
            }

            var priorProposals = await dbContext.WorkoutPlanProposalsV02
                .Where(item => item.UserId == userId && item.PlanId == snapshot.PlanId &&
                               item.Status == ProposedStatus &&
                               item.RulesetVersion == PhaseReviewRule.Version)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var priorProposal in priorProposals)
            {
                priorProposal.Status = ExpiredStatus;
                priorProposal.DecidedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                priorProposal.ConcurrencyVersion = checked(priorProposal.ConcurrencyVersion +
                                                            ConcurrencyVersionIncrement);
            }

            var proposal = new WorkoutPlanProposalV02Record
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PlanId = snapshot.PlanId,
                ExpectedRevisionId = snapshot.SourceRevisionId,
                RulesetVersion = PhaseReviewRule.Version,
                TrainingPolicyVersion = StrengthPlanPreviewRule.Version,
                Status = ProposedStatus,
                InputSnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions),
                ProposalJson = JsonSerializer.Serialize(preview, JsonOptions),
                ReasonsJson = JsonSerializer.Serialize(new[] { preview.Tier.ToString() }, JsonOptions),
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            dbContext.WorkoutPlanProposalsV02.Add(proposal);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return proposal.Id;
        }).ConfigureAwait(false);

    public async Task<DecidePhaseAdjustmentProposalResultV010> DecideAsync(Guid userId,
        PhaseAdjustmentProposalCommandV010 command, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var proposal = await dbContext.WorkoutPlanProposalsV02.SingleOrDefaultAsync(
                item => item.UserId == userId && item.Id == command.ProposalId,
                cancellationToken).ConfigureAwait(false);
            if (proposal is null)
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010(NotFoundStatus, false, ProposalNotFoundCode),
                    cancellationToken).ConfigureAwait(false);
            if (proposal.Status != ProposedStatus)
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010(ConflictStatus, false, AlreadyDecidedCode),
                    cancellationToken).ConfigureAwait(false);

            if (!command.ApplyChanges)
            {
                proposal.Status = RejectedStatus;
                proposal.DecidedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                proposal.ConcurrencyVersion = checked(proposal.ConcurrencyVersion + ConcurrencyVersionIncrement);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010(RejectedStatus, false, null),
                    cancellationToken).ConfigureAwait(false);
            }

            var persistedSnapshot = Deserialize<PhaseAdjustmentProposalSnapshotV010>(
                proposal.InputSnapshotJson);
            if (persistedSnapshot is null || string.IsNullOrWhiteSpace(persistedSnapshot.TimeZoneId))
                return await ExpireAsync(transaction, proposal, cancellationToken).ConfigureAwait(false);

            var loaded = await source.LoadAsync(userId, persistedSnapshot.TimeZoneId, cancellationToken)
                .ConfigureAwait(false);
            if (loaded is null)
                return await ExpireAsync(transaction, proposal, cancellationToken).ConfigureAwait(false);

            var gate = PhaseReviewRule.Evaluate(loaded.GateInput);
            var preview = PhaseAdjustmentRule.Preview(gate, loaded.GateInput.Baseline,
                loaded.GateInput.BaselineScale, loaded.AcceptedSessions);
            var snapshot = PhaseAdjustmentProposalSnapshotV010.Create(loaded, gate);
            var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            var previewJson = JsonSerializer.Serialize(preview, JsonOptions);
            var reasonJson = JsonSerializer.Serialize(new[] { preview.Tier.ToString() }, JsonOptions);
            var activeRevisionId = await LatestRevisionIdAsync(userId, proposal.PlanId, cancellationToken)
                .ConfigureAwait(false);
            if (proposal.RulesetVersion != PhaseReviewRule.Version ||
                proposal.TrainingPolicyVersion != StrengthPlanPreviewRule.Version ||
                proposal.PlanId != snapshot.PlanId ||
                proposal.ExpectedRevisionId != snapshot.SourceRevisionId ||
                proposal.ExpectedRevisionId != activeRevisionId ||
                proposal.InputSnapshotJson != snapshotJson ||
                proposal.ProposalJson != previewJson || proposal.ReasonsJson != reasonJson ||
                gate.Status != PhaseReviewGateStatus.ReadyForAdjustmentPreview ||
                preview.Status != PhaseAdjustmentPreviewStatus.ChangesOrChoicesAvailable)
                return await ExpireAsync(transaction, proposal, cancellationToken).ConfigureAwait(false);

            var application = PhaseAdjustmentRule.Apply(gate, preview, loaded.AcceptedSessions,
                command.Selections, applyChanges: true);
            if (application.Status == PhaseAdjustmentApplicationStatus.InvalidInput)
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010(InvalidInputStatus, false,
                        application.LimitationCode), cancellationToken).ConfigureAwait(false);
            if (application.Status != PhaseAdjustmentApplicationStatus.Applied)
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010(ConflictStatus, false, NoChangesCode),
                    cancellationToken).ConfigureAwait(false);

            if (!HasEffectiveChanges(loaded.AcceptedSessions, application.Sessions))
            {
                proposal.Status = RejectedStatus;
                proposal.DecidedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                proposal.ConcurrencyVersion = checked(proposal.ConcurrencyVersion + ConcurrencyVersionIncrement);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await FinishAsync(transaction,
                    new DecidePhaseAdjustmentProposalResultV010("NoChanges", false, NoChangesCode),
                    cancellationToken).ConfigureAwait(false);
            }

            await ApplySessionChangesAsync(userId, proposal.PlanId,
                loaded.AcceptedSessions, application.Sessions, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var allDays = await dbContext.WorkoutDays.AsNoTracking()
                .Where(day => day.UserId == userId && day.WorkoutPlanId == proposal.PlanId)
                .OrderBy(day => day.Date).ThenBy(day => day.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var dayIds = allDays.Select(day => day.Id).ToArray();
            var allExercises = dayIds.Length == MinimumOptionalSetCount
                ? []
                : await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(exercise => exercise.UserId == userId && dayIds.Contains(exercise.WorkoutDayId))
                    .OrderBy(exercise => exercise.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
            var nextRevisionNumber = checked(
                (await dbContext.WorkoutPlanRevisionsV02
                    .Where(revision => revision.UserId == userId && revision.PlanId == proposal.PlanId)
                    .Select(revision => (long?)revision.RevisionNumber)
                    .MaxAsync(cancellationToken).ConfigureAwait(false) ?? NoPreviousRevision) + RevisionNumberIncrement);
            var revision = new WorkoutPlanRevisionV02Record
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PlanId = proposal.PlanId,
                RevisionNumber = nextRevisionNumber,
                SourceProposalId = proposal.Id,
                RulesetVersion = PhaseAdjustmentRule.Version,
                TrainingPolicyVersion = StrengthPlanPreviewRule.Version,
                PlanJson = JsonSerializer.Serialize(new { Days = allDays, Exercises = allExercises },
                    JsonOptions),
                InputSnapshotJson = proposal.InputSnapshotJson,
                AcceptedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            proposal.Status = AcceptedStatus;
            proposal.DecidedAtUtc = revision.AcceptedAtUtc;
            proposal.AcceptedRevisionId = revision.Id;
            proposal.ConcurrencyVersion = checked(proposal.ConcurrencyVersion + ConcurrencyVersionIncrement);
            dbContext.WorkoutPlanRevisionsV02.Add(revision);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FinishAsync(transaction,
                new DecidePhaseAdjustmentProposalResultV010(AcceptedStatus, true, null),
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

    private async Task ApplySessionChangesAsync(Guid userId, Guid planId,
        IReadOnlyList<PhaseAdjustmentSession> acceptedSessions,
        IReadOnlyList<PhaseAdjustmentSession> adjustedSessions,
        CancellationToken cancellationToken)
    {
        var acceptedById = acceptedSessions.ToDictionary(session => session.SessionId);
        foreach (var adjusted in adjustedSessions)
        {
            var accepted = acceptedById[adjusted.SessionId];
            if (adjusted.IsSkipped || !string.IsNullOrWhiteSpace(adjusted.SelectedReplacementTemplateId))
            {
                var original = await dbContext.WorkoutDays.SingleAsync(day => day.UserId == userId &&
                    day.WorkoutPlanId == planId && day.Id == adjusted.SessionId,
                    cancellationToken).ConfigureAwait(false);
                original.IsRemovedByUserAdjustment = true;
                if (!string.IsNullOrWhiteSpace(adjusted.SelectedReplacementTemplateId))
                {
                    if (!Guid.TryParse(adjusted.SelectedReplacementTemplateId, out var replacementId))
                        throw new InvalidOperationException("Phase proposal contains an invalid template identifier.");
                    var replacement = await dbContext.WorkoutDays.SingleAsync(day => day.UserId == userId &&
                        day.WorkoutPlanId == planId && day.Id == replacementId &&
                        day.Date == original.Date && day.IsOptional && day.IsEasierAlternative,
                        cancellationToken).ConfigureAwait(false);
                    replacement.IsOptional = false;
                    replacement.IsEasierAlternative = false;
                }
            }

            for (var exerciseIndex = 0; exerciseIndex < adjusted.Exercises.Count; exerciseIndex++)
            {
                var updatedExercise = adjusted.Exercises[exerciseIndex];
                var previousExercise = accepted.Exercises.SingleOrDefault(exercise =>
                    exercise.PrescriptionId == updatedExercise.PrescriptionId);
                if (previousExercise is null || previousExercise.AcceptedSets == updatedExercise.AcceptedSets)
                    continue;
                var exerciseRecord = await dbContext.WorkoutDayExercises.SingleAsync(exercise =>
                    exercise.UserId == userId && exercise.WorkoutDayId == adjusted.SessionId &&
                    exercise.Id == updatedExercise.PrescriptionId,
                    cancellationToken).ConfigureAwait(false);
                exerciseRecord.Sets = updatedExercise.AcceptedSets;
                if (exerciseRecord.OptionalSets >= exerciseRecord.Sets)
                    exerciseRecord.OptionalSets = Math.Max(MinimumOptionalSetCount,
                        exerciseRecord.Sets - MinimumRequiredSetCount);
            }

            if (accepted.ContinuousMainMinutes != adjusted.ContinuousMainMinutes &&
                adjusted.ContinuousMainMinutes is { } mainMinutes)
            {
                var day = await dbContext.WorkoutDays.SingleAsync(record => record.UserId == userId &&
                    record.WorkoutPlanId == planId && record.Id == adjusted.SessionId,
                    cancellationToken).ConfigureAwait(false);
                day.PlannedMainDurationMinutes = mainMinutes;
            }
        }
    }

    private Task<Guid?> LatestRevisionIdAsync(Guid userId, Guid planId,
        CancellationToken cancellationToken) => dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
        .Where(revision => revision.UserId == userId && revision.PlanId == planId)
        .OrderByDescending(revision => revision.RevisionNumber)
        .Select(revision => (Guid?)revision.Id)
        .FirstOrDefaultAsync(cancellationToken);

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

    private static bool HasEffectiveChanges(IReadOnlyList<PhaseAdjustmentSession> acceptedSessions,
        IReadOnlyList<PhaseAdjustmentSession> adjustedSessions)
    {
        var acceptedById = acceptedSessions.ToDictionary(session => session.SessionId);
        foreach (var adjusted in adjustedSessions)
        {
            var accepted = acceptedById[adjusted.SessionId];
            if (adjusted.IsSkipped || !string.IsNullOrWhiteSpace(adjusted.SelectedReplacementTemplateId) ||
                adjusted.ContinuousMainMinutes != accepted.ContinuousMainMinutes)
                return true;
            foreach (var exercise in adjusted.Exercises)
            {
                var previous = accepted.Exercises.SingleOrDefault(item =>
                    item.PrescriptionId == exercise.PrescriptionId);
                if (previous is not null && previous.AcceptedSets != exercise.AcceptedSets)
                    return true;
            }
        }
        return false;
    }

    private async Task<DecidePhaseAdjustmentProposalResultV010> ExpireAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        WorkoutPlanProposalV02Record proposal, CancellationToken cancellationToken)
    {
        proposal.Status = ExpiredStatus;
        proposal.DecidedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        proposal.ConcurrencyVersion = checked(proposal.ConcurrencyVersion + ConcurrencyVersionIncrement);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await FinishAsync(transaction,
            new DecidePhaseAdjustmentProposalResultV010(ExpiredStatus, false, PhaseSnapshotStaleCode),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DecidePhaseAdjustmentProposalResultV010> FinishAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        DecidePhaseAdjustmentProposalResultV010 result, CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
