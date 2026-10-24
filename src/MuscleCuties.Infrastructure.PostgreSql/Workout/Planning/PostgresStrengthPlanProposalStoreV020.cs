using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Stores immutable v0.2 strength proposals under the authenticated tenant context.</summary>
public sealed class PostgresStrengthPlanProposalStoreV020(PostgresDbContext dbContext)
    : IStrengthPlanProposalStoreV020
{
    private const int NoProposalSessions = 0;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> SaveAsync(Guid userId, StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewResponseV020 preview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(preview);
        if (userId == Guid.Empty ||
            !string.Equals(preview.Status, "Feasible", StringComparison.Ordinal) ||
            preview.Sessions.Count == NoProposalSessions ||
            preview.PolicyId != StrengthPlanPreviewRule.PolicyId ||
            preview.PolicyVersion != StrengthPlanPreviewRule.Version ||
            preview.RulesetVersion != StrengthPlanPreviewRule.RulesetVersion)
            throw new ArgumentException("Only a feasible proposal from the active training policy can be saved.");

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

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
            var activeRevisionId = activePlanId is { } planId
                ? await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                    .Where(revision => revision.UserId == userId && revision.PlanId == planId)
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => (Guid?)revision.Id)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                : null;

            var snapshotJson = JsonSerializer.Serialize(
                new ProposalInputSnapshot(request, profileVersion.Value, activePlanId, activeRevisionId), JsonOptions);
            var previewJson = JsonSerializer.Serialize(preview, JsonOptions);
            var existingProposalId = await dbContext.WorkoutPlanProposalsV02.AsNoTracking()
                .Where(proposal => proposal.UserId == userId && proposal.Status == "Proposed" &&
                                   proposal.InputSnapshotJson == snapshotJson &&
                                   proposal.ProposalJson == previewJson)
                .OrderByDescending(proposal => proposal.CreatedAtUtc)
                .Select(proposal => (Guid?)proposal.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (existingProposalId is { } existingId)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return existingId;
            }

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
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.WorkoutPlanProposalsV02.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return record.Id;
        }).ConfigureAwait(false);
    }

    private sealed record ProposalInputSnapshot(
        StrengthPlanPreviewRequestV020 Request,
        int ProfileVersion,
        Guid? BaselineActivePlanId,
        Guid? BaselineRevisionId);
}
