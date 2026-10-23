namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public interface IS3PresignedUrlService
{
    string GeneratePresignedUploadUrl(string s3Key, string contentType, TimeSpan expiration);
    S3PresignedPost GeneratePresignedUploadPost(string s3Key, string contentType, string uploadIntentId, string sha256Checksum, TimeSpan expiration);
    string GeneratePresignedDownloadUrl(string s3Key, TimeSpan expiration);
    string GeneratePresignedDownloadUrl(string s3Key, string versionId, TimeSpan expiration) =>
        throw new InvalidOperationException("Version-pinned photo downloads are not configured.");
    string GeneratePresignedDeleteUrl(string s3Key, TimeSpan expiration);
}

public sealed record S3PresignedPost(string Url, IReadOnlyDictionary<string, string> Fields, DateTime ExpiresAtUtc);
