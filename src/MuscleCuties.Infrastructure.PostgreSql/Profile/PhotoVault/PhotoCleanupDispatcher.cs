namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Explicitly invoked bounded dispatch over due owners. Discovery and owner processing use separate scopes.</summary>
public sealed class PhotoCleanupDispatcher(
    IDuePhotoCleanupOwnerDiscovery discovery,
    IPhotoCleanupOwnerProcessorRunner processorRunner,
    TimeProvider timeProvider)
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;
    private const int MinimumPagesPerRun = 1;
    private const int MaximumPagesPerRun = 20;
    private const int MinimumOwnerBatchSize = 1;
    private const int MaximumOwnerBatchSize = 100;
    private readonly SemaphoreSlim runLock = new(1, 1);
    private Guid? continuationOwnerId;

    public async Task<PhotoCleanupDispatchResult> DispatchAsync(
        int pageSize,
        int maximumPages,
        int ownerBatchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default,
        Guid? initialAfterOwnerId = null,
        Func<Guid?, CancellationToken, Task>? persistCursor = null)
    {
        if (pageSize is < MinimumPageSize or > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (maximumPages is < MinimumPagesPerRun or > MaximumPagesPerRun) throw new ArgumentOutOfRangeException(nameof(maximumPages));
        if (ownerBatchSize is < MinimumOwnerBatchSize or > MaximumOwnerBatchSize) throw new ArgumentOutOfRangeException(nameof(ownerBatchSize));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var afterOwnerId = initialAfterOwnerId ?? continuationOwnerId;
            var pages = 0;
            var visited = new HashSet<Guid>();
            var processed = 0;
            var failed = 0;
            var claimed = 0;
            var completed = 0;
            var retried = 0;
            var fenced = 0;
            var wrapped = false;

            while (pages < maximumPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var owners = await discovery.DiscoverDueOwnersAsync(
                    timeProvider.GetUtcNow().UtcDateTime, afterOwnerId, pageSize, cancellationToken).ConfigureAwait(false);
                pages++;
                if (owners.Count == 0)
                {
                    if (afterOwnerId is not null && !wrapped)
                    {
                        afterOwnerId = null;
                        continuationOwnerId = null;
                        if (persistCursor is not null) await persistCursor(null, cancellationToken).ConfigureAwait(false);
                        wrapped = true;
                        continue;
                    }
                    break;
                }

                var completedCycle = false;
                foreach (var ownerId in owners)
                {
                    if (visited.Contains(ownerId))
                    {
                        completedCycle = true;
                        break;
                    }
                    visited.Add(ownerId);
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var result = await processorRunner.ProcessOwnerAsync(ownerId, ownerBatchSize, leaseDuration, cancellationToken).ConfigureAwait(false);
                        processed++;
                        claimed += result.Claimed;
                        completed += result.Completed;
                        retried += result.Retried;
                        fenced += result.Fenced;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // A failed owner remains independently retryable; continue to avoid starving later owners.
                        failed++;
                    }
                    afterOwnerId = ownerId;
                    continuationOwnerId = ownerId;
                    if (persistCursor is not null) await persistCursor(afterOwnerId, cancellationToken).ConfigureAwait(false);
                }

                if (completedCycle) break;

                if (owners.Count < pageSize)
                {
                    if (!wrapped)
                    {
                        afterOwnerId = null;
                        continuationOwnerId = null;
                        if (persistCursor is not null) await persistCursor(null, cancellationToken).ConfigureAwait(false);
                        wrapped = true;
                        continue;
                    }
                    break;
                }
            }

            return new PhotoCleanupDispatchResult(pages, processed, failed, claimed, completed, retried, fenced);
        }
        finally
        {
            runLock.Release();
        }
    }
}

/// <summary>Creates and disposes an owner-scoped processor context for each invocation.</summary>
public interface IPhotoCleanupOwnerProcessorRunner
{
    Task<PhotoCleanupBatchResult> ProcessOwnerAsync(Guid ownerId, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken);
}

public sealed record PhotoCleanupDispatchResult(
    int Pages,
    int OwnersProcessed,
    int OwnersFailed,
    int Claimed,
    int Completed,
    int Retried,
    int Fenced);
