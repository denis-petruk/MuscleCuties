using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public interface IServerPhotoVaultStore
{
    Task<PhotoUploadIssueResponse> RequestUploadUrlAsync(
        Guid userId,
        PhotoUploadUrlRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitPhotoMetadataResponse> CommitPhotoMetadataAsync(
        Guid userId,
        CommitPhotoMetadataRequest request,
        CancellationToken cancellationToken = default);

    Task<PhotoHistoryResponse> GetPhotoHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<PhotoDownloadUrlResponse?> GetPhotoDownloadUrlAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePhotoAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for owner-scoped, idempotent S3 upload intents.</summary>
public interface IPhotoUploadIntentStore
{
    Task<Contracts.PhotoUploadIntentDto> CreateOrGetAsync(
        Guid userId,
        Contracts.CreatePhotoUploadIntentRequest request,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<Contracts.PhotoUploadIntentDto?> GetAsync(
        Guid userId,
        Guid photoId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<Contracts.PhotoUploadIntentDto?> TransitionAsync(
        Guid userId,
        Guid photoId,
        string expectedS3Key,
        Contracts.PhotoUploadIntentState targetState,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
