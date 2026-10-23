namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>Trusted, owner-scoped context loaded from the persisted photo upload intent.</summary>
public sealed record PhotoUploadObjectContext(
    Guid OwnerId,
    Guid PhotoId,
    string Bucket,
    string Key,
    string ExpectedSha256ChecksumHex,
    long DeclaredSizeBytes,
    string ExpectedContentType);

/// <summary>Metadata measured from S3 for one exact uploaded object.</summary>
public sealed record S3PhotoObjectInspection(
    long ContentLength,
    string? ContentType,
    string? UploadIntentId,
    string Sha256ChecksumBase64,
    string? VersionId,
    ReadOnlyMemory<byte>? ObjectBytes = null);

public enum S3PhotoObjectInspectionFailure
{
    NotFound,
    Forbidden,
    Transient,
    InvalidResponse
}

public sealed class S3PhotoObjectInspectionException(
    S3PhotoObjectInspectionFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public S3PhotoObjectInspectionFailure Failure { get; } = failure;
}

/// <summary>Reads metadata for the exact S3 object identified by an owner-bound persisted intent.</summary>
public interface IS3PhotoObjectInspector
{
    Task<S3PhotoObjectInspection> InspectAsync(
        PhotoUploadObjectContext context,
        CancellationToken cancellationToken = default);

    Task<ReadOnlyMemory<byte>> ReadVersionAsync(PhotoUploadObjectContext context, string versionId,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Exact-version photo reads are not configured.");

    Task<S3PhotoPublication> PublishAsync(PhotoUploadObjectContext context, SanitizedPhotoBytes photo,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Immutable photo publication is not configured.");
}

public sealed record S3PhotoPublication(string Key, string VersionId, string Sha256Checksum);
