using Microsoft.Extensions.Configuration;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>One explicitly invoked, bounded pass over one owner's durable photo cleanup queue.</summary>
public sealed class PhotoCleanupProcessor(
    PostgresPhotoCleanupStore store,
    IS3PhotoObjectCleanup s3Cleanup,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private const int MaximumBatchSize = 100;
    private const int MaximumBackoffMinutes = 24 * 60;
    private const int MaximumBackoffExponent = 10;
    private const string DefaultBucket = "musclecuties-media";

    public async Task<PhotoCleanupBatchResult> ProcessOwnerAsync(
        Guid ownerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A non-empty owner is required.", nameof(ownerId));
        if (batchSize is <= 0 or > MaximumBatchSize) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var startedAt = timeProvider.GetUtcNow().UtcDateTime;
        var items = await store.ClaimDueAsync(ownerId, startedAt, leaseDuration, batchSize, cancellationToken).ConfigureAwait(false);
        var completed = 0;
        var retried = 0;
        var fenced = 0;
        var bucket = configuration["Storage:S3:BucketName"] ?? DefaultBucket;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await s3Cleanup.DeleteAllVersionsAsync(
                    new PhotoObjectCleanupTarget(item.UserId, item.PhotoId, bucket, item.S3Key), cancellationToken).ConfigureAwait(false);
                if (await store.CompleteAsync(item, timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false)) completed++;
                else fenced++;
            }
            catch (S3PhotoObjectCleanupException exception) when (!cancellationToken.IsCancellationRequested)
            {
                var category = exception.Failure switch
                {
                    S3PhotoObjectCleanupFailure.Forbidden => PhotoCleanupFailureCategory.Permission,
                    S3PhotoObjectCleanupFailure.Configuration => PhotoCleanupFailureCategory.Configuration,
                    _ => PhotoCleanupFailureCategory.Transient
                };
                var attemptedAt = timeProvider.GetUtcNow().UtcDateTime;
                var retryAt = attemptedAt.AddMinutes(BackoffMinutes(item.AttemptCount));
                if (await store.RetryAsync(item, attemptedAt, retryAt, category, cancellationToken).ConfigureAwait(false)) retried++;
                else fenced++;
            }
        }
        return new PhotoCleanupBatchResult(items.Count, completed, retried, fenced);
    }

    private static int BackoffMinutes(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, MaximumBackoffExponent);
        return Math.Min(1 << exponent, MaximumBackoffMinutes);
    }
}

public sealed record PhotoCleanupBatchResult(int Claimed, int Completed, int Retried, int Fenced);
