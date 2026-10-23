using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public sealed class ServerPhotoVaultUseCase(IServerPhotoVaultStore store)
{
    private const long MinimumFileSizeBytes = 1;
    private const long MaximumFileSizeBytes = 15 * 1024 * 1024; // 15 Megabytes
    private const int MinimumHistoryLimit = 1;
    private const int MaximumHistoryLimit = 100;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/heic",
        "image/webp"
    };

    public Task<PhotoUploadIssueResponse> RequestUploadUrlAsync(
        Guid userId,
        PhotoUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ContentType) || !AllowedContentTypes.Contains(request.ContentType.Trim()))
            throw new ArgumentException($"Invalid photo content type '{request.ContentType}'. Allowed formats: JPEG, PNG, HEIC, WEBP.", nameof(request));

        if (request.FileSizeBytes is < MinimumFileSizeBytes or > MaximumFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(request), $"File size must be between {MinimumFileSizeBytes} bytes and {MaximumFileSizeBytes} bytes (15 MB).");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw new ArgumentException("A non-empty idempotency key of at most 128 characters is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Sha256Checksum))
            throw new ArgumentException("A SHA-256 checksum is required.", nameof(request));

        return store.RequestUploadUrlAsync(userId, request, cancellationToken);
    }

    public Task<CommitPhotoMetadataResponse> CommitPhotoMetadataAsync(
        Guid userId,
        CommitPhotoMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        ArgumentNullException.ThrowIfNull(request);

        if (request.PhotoId == Guid.Empty)
            throw new ArgumentException("Photo identifier must not be empty.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.S3Key))
            throw new ArgumentException("S3 object key must not be empty.", nameof(request));

        return store.CommitPhotoMetadataAsync(userId, request, cancellationToken);
    }

    public Task<PhotoHistoryResponse> GetPhotoHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        var safeLimit = Math.Clamp(limit, MinimumHistoryLimit, MaximumHistoryLimit);
        var safeOffset = Math.Max(0, offset);

        return store.GetPhotoHistoryAsync(userId, safeLimit, safeOffset, cancellationToken);
    }

    public Task<PhotoDownloadUrlResponse?> GetPhotoDownloadUrlAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        if (photoId == Guid.Empty)
            throw new ArgumentException("Photo identifier must not be empty.", nameof(photoId));

        return store.GetPhotoDownloadUrlAsync(userId, photoId, cancellationToken);
    }

    public Task<bool> DeletePhotoAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        if (photoId == Guid.Empty)
            throw new ArgumentException("Photo identifier must not be empty.", nameof(photoId));

        return store.DeletePhotoAsync(userId, photoId, cancellationToken);
    }
}
