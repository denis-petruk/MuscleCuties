using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>The presigner used when S3 is not configured: every call reports unavailability instead of throwing a null reference.</summary>
public sealed class UnavailableS3PresignedUrlService : IS3PresignedUrlService
{
    private const string UnavailableMessage = "Photo storage is unavailable because Storage:S3:BucketName is not configured.";

    /// <summary>Refuses to sign an upload because photo storage is not configured.</summary>
    public string GeneratePresignedUploadUrl(string s3Key, string contentType, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    /// <summary>Refuses to sign a direct upload for the same reason.</summary>
    public S3PresignedPost GeneratePresignedUploadPost(string s3Key, string contentType, string uploadIntentId, string sha256Checksum, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    /// <summary>Refuses to sign a download because photo storage is not configured.</summary>
    public string GeneratePresignedDownloadUrl(string s3Key, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    /// <summary>Refuses to sign a versioned download for the same reason.</summary>
    public string GeneratePresignedDownloadUrl(string s3Key, string versionId, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    /// <summary>Refuses to sign a delete for the same reason.</summary>
    public string GeneratePresignedDeleteUrl(string s3Key, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);
}

/// <summary>The inspector used when S3 is not configured, answering that photos are unavailable.</summary>
public sealed class UnavailableS3PhotoObjectInspector : IS3PhotoObjectInspector
{
    private const string UnavailableMessage = "Photo storage is unavailable because Storage:S3:BucketName is not configured.";

    /// <summary>Reports that the object cannot be inspected.</summary>
    public Task<S3PhotoObjectInspection> InspectAsync(
        PhotoUploadObjectContext context,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(UnavailableMessage);
}
