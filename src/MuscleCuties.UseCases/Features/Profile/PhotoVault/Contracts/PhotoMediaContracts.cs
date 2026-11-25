namespace MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

/// <summary>A request for a URL to upload a photo to.</summary>
public sealed record PhotoUploadUrlRequest(
    string ContentType,
    long FileSizeBytes,
    string? Pose = null,
    string? IdempotencyKey = null,
    string? Sha256Checksum = null);

/// <summary>The upload URL with the limits it may be used under.</summary>
public sealed record PhotoUploadIssueResponse(
    Guid UploadIntentId,
    Guid PhotoId,
    string S3Key,
    string UploadUrl,
    IReadOnlyDictionary<string, string> Fields,
    DateTime ExpiresAtUtc);

/// <summary>The metadata that turns an authorised upload into a photo.</summary>
public sealed record CommitPhotoMetadataRequest(
    Guid PhotoId,
    DateOnly Date,
    string S3Key,
    decimal? WeightKg = null,
    string? Notes = null,
    string? Phase = null,
    string? Pose = null);

/// <summary>The stored photo, so the client can show it immediately.</summary>
public sealed record CommitPhotoMetadataResponse(
    bool Success,
    Guid PhotoId,
    string Message);

/// <summary>One photo in the journey.</summary>
public sealed record PhotoItemDto(
    Guid Id,
    DateOnly Date,
    string S3Key,
    string PresignedDownloadUrl,
    decimal? WeightKg,
    string Notes,
    string Phase,
    DateTime CreatedAt);

/// <summary>Her photos, newest first.</summary>
public sealed record PhotoHistoryResponse(
    IReadOnlyList<PhotoItemDto> Photos,
    int TotalCount);

/// <summary>A short-lived URL for viewing one photo.</summary>
public sealed record PhotoDownloadUrlResponse(
    Guid PhotoId,
    string PresignedDownloadUrl,
    DateTime ExpiresAtUtc);

/// <summary>A request to authorise an upload, idempotent on its key.</summary>
public sealed record CreatePhotoUploadIntentRequest(
    string IdempotencyKey,
    string ContentType,
    long DeclaredSizeBytes,
    string Sha256Checksum);

/// <summary>Where an upload intent stands, which decides whether it may still be used.</summary>
public enum PhotoUploadIntentState
{
    Pending = 0,
    Committed = 1,
    Cancelled = 2,
    Expired = 3,
    Deleted = 4
}

/// <summary>An authorised upload as the client sees it.</summary>
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
