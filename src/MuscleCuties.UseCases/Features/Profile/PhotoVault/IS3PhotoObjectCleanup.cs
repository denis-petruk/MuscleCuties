namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>Trusted cleanup target loaded from a persisted, leased photo cleanup row.</summary>
public sealed record PhotoObjectCleanupTarget(Guid OwnerId, Guid PhotoId, string Bucket, string Key);

/// <summary>Why a stored object could not be removed.</summary>
public enum S3PhotoObjectCleanupFailure
{
    Transient,
    Forbidden,
    Configuration
}

/// <summary>A cleanup that failed, raised so the deletion path can retry rather than report success.</summary>
public sealed class S3PhotoObjectCleanupException(
    S3PhotoObjectCleanupFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>Why the object could not be removed.</summary>
    public S3PhotoObjectCleanupFailure Failure { get; } = failure;
}

/// <summary>Permanently removes every version and delete marker for one owner-bound photo key.</summary>
public interface IS3PhotoObjectCleanup
{
    Task DeleteAllVersionsAsync(PhotoObjectCleanupTarget target, CancellationToken cancellationToken = default);
}
