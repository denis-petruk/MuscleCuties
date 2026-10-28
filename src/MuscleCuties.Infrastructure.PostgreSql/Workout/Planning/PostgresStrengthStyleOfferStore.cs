using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresStrengthStyleOfferStore(PostgresDbContext context) : IStrengthStyleOfferStore
{
    // Called with the tenant transaction and account lock held, after automatic acceptance succeeds.
    public async Task CreateInCurrentTransactionAsync(Guid userId, Guid planId, Guid revisionId, int currentStyleId,
        IReadOnlyList<int> fittingStyleIds, CancellationToken cancellationToken)
    {
        var styles = fittingStyleIds.Distinct().Where(id => Enum.IsDefined((WorkoutActivityType)id) &&
            StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)id)).ToArray();
        if (!styles.Contains(currentStyleId)) return;
        var alternatives = styles.Where(id => id != currentStyleId).ToArray();
        if (alternatives.Length == 0) return;
        if (await context.Set<StrengthStyleOfferRecord>().AnyAsync(row => row.UserId == userId && row.RevisionId == revisionId,
                cancellationToken).ConfigureAwait(false)) return;
        context.Set<StrengthStyleOfferRecord>().Add(new StrengthStyleOfferRecord
        {
            Id = Guid.NewGuid(), UserId = userId, PlanId = planId, RevisionId = revisionId,
            CurrentStyleId = currentStyleId, AlternativeStyleIds = alternatives
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<StrengthStyleOfferDto?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken) =>
        WithLockAsync(userId, async () =>
        {
            var row = await CurrentAsync(userId, cancellationToken).ConfigureAwait(false);
            return row is null ? null : new StrengthStyleOfferDto(row.Id, row.PlanId, row.RevisionId,
                row.CurrentStyleId, row.AlternativeStyleIds);
        }, cancellationToken);

    public Task<bool> DismissAsync(Guid userId, Guid offerId, CancellationToken cancellationToken) =>
        WithLockAsync(userId, async () =>
        {
            var row = await CurrentAsync(userId, cancellationToken).ConfigureAwait(false);
            if (row is null || row.Id != offerId) return false;
            row.DismissedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private async Task<StrengthStyleOfferRecord?> CurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var state = await context.WorkoutAccountPlanningStates.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (state is null || state.PromotionPending || state.SafetyRegenerationPending) return null;
        var planId = await context.WorkoutPlans.AsNoTracking().Where(row => row.UserId == userId && row.IsActive)
            .OrderByDescending(row => row.CreatedAt).Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (planId is null) return null;
        var revisionId = await context.WorkoutPlanRevisionsV02.AsNoTracking().Where(row => row.UserId == userId && row.PlanId == planId)
            .OrderByDescending(row => row.RevisionNumber).Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return await context.Set<StrengthStyleOfferRecord>().SingleOrDefaultAsync(row => row.UserId == userId &&
            row.PlanId == planId && row.RevisionId == revisionId && row.DismissedAtUtc == null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithLockAsync<T>(Guid userId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);
            var result = await action().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);
    }
}
