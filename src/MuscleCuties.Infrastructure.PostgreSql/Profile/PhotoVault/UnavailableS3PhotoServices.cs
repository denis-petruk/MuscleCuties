using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class UnavailableS3PresignedUrlService : IS3PresignedUrlService
{
    private const string UnavailableMessage = "Photo storage is unavailable because Storage:S3:BucketName is not configured.";

    public string GeneratePresignedUploadUrl(string s3Key, string contentType, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    public S3PresignedPost GeneratePresignedUploadPost(string s3Key, string contentType, string uploadIntentId, string sha256Checksum, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    public string GeneratePresignedDownloadUrl(string s3Key, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    public string GeneratePresignedDownloadUrl(string s3Key, string versionId, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);

    public string GeneratePresignedDeleteUrl(string s3Key, TimeSpan expiration) =>
        throw new InvalidOperationException(UnavailableMessage);
}

public sealed class UnavailableS3PhotoObjectInspector : IS3PhotoObjectInspector
{
    private const string UnavailableMessage = "Photo storage is unavailable because Storage:S3:BucketName is not configured.";

    public Task<S3PhotoObjectInspection> InspectAsync(
        PhotoUploadObjectContext context,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(UnavailableMessage);
}
