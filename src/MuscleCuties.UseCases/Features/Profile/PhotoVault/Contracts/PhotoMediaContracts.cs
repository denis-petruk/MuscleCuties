namespace MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

public sealed record PhotoUploadUrlRequest(
    string ContentType,
    long FileSizeBytes,
    string? Pose = null,
    string? IdempotencyKey = null,
    string? Sha256Checksum = null);

public sealed record PhotoUploadIssueResponse(
    Guid UploadIntentId,
    Guid PhotoId,
    string S3Key,
    string UploadUrl,
    IReadOnlyDictionary<string, string> Fields,
    DateTime ExpiresAtUtc);

public sealed record CommitPhotoMetadataRequest(
    Guid PhotoId,
    DateOnly Date,
    string S3Key,
    decimal? WeightKg = null,
    string? Notes = null,
    string? Phase = null,
    string? Pose = null);

public sealed record CommitPhotoMetadataResponse(
    bool Success,
    Guid PhotoId,
    string Message);

public sealed record PhotoItemDto(
    Guid Id,
    DateOnly Date,
    string S3Key,
    string PresignedDownloadUrl,
    decimal? WeightKg,
    string Notes,
    string Phase,
    DateTime CreatedAt);

public sealed record PhotoHistoryResponse(
    IReadOnlyList<PhotoItemDto> Photos,
    int TotalCount);

public sealed record PhotoDownloadUrlResponse(
    Guid PhotoId,
    string PresignedDownloadUrl,
    DateTime ExpiresAtUtc);

public sealed record CreatePhotoUploadIntentRequest(
    string IdempotencyKey,
    string ContentType,
    long DeclaredSizeBytes,
    string Sha256Checksum);

public enum PhotoUploadIntentState
{
    Pending = 0,
    Committed = 1,
    Cancelled = 2,
    Expired = 3,
    Deleted = 4
}

public sealed record PhotoUploadIntentDto(
    Guid PhotoId,
    string S3Key,
    string IdempotencyKey,
    string ContentType,
    long DeclaredSizeBytes,
    string Sha256Checksum,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    PhotoUploadIntentState State);
