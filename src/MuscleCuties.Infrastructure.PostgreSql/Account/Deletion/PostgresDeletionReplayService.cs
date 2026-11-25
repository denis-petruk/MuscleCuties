using MuscleCuties.UseCases.Features.Account.Deletion;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;

/// <summary>Replays a deletion request that was recorded but may not have completed, so a retried request cannot leave a half-deleted account.</summary>
public sealed class PostgresDeletionReplayService(
    IAccountDeletionAuditStore auditStore,
    IServerAccountPurgeStore purgeStore) : IDeletionReplayService
{
    /// <summary>Retries the deletions that were recorded but not completed.</summary>
    public async Task<DeletionReplayResult> ReplayPendingDeletionsAsync(
        CancellationToken cancellationToken = default)
    {
        var tombstones = await auditStore.GetDeletionAuditsAsync(cancellationToken).ConfigureAwait(false);
        var evaluatedCount = tombstones.Count;
        var rePurgedCount = 0;
        var totalResurrectedPurged = 0;
        var rePurgedUserIds = new List<Guid>();

        foreach (var tombstone in tombstones)
        {
            // Always execute the inventory-checked purge: tenant rows can outlive their user/profile parents.
            var purgeResult = await purgeStore
                .PurgeAccountDataAsync(tombstone.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (purgeResult.Succeeded && purgeResult.PurgedRecordCount > 0)
            {
                rePurgedCount++;
                totalResurrectedPurged += purgeResult.PurgedRecordCount;
                rePurgedUserIds.Add(tombstone.UserId);
            }
        }

        return new DeletionReplayResult(
            AccountsEvaluated: evaluatedCount,
            AccountsRePurged: rePurgedCount,
            TotalResurrectedRecordsPurged: totalResurrectedPurged,
            RePurgedUserIds: rePurgedUserIds);
    }
}
