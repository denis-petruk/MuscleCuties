using System.Net.Http.Headers;
using System.Security.Cryptography;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.UI.Services.Profile;

public sealed class MobilePhotoVaultStore(
    IMuscleCutiesBackendApiClient apiClient,
    HttpClient photoTransferClient) : IPhotoVaultStore
{
    private const string PhotosRoute = "/api/v1/photos";
    private const string JpegContentType = "image/jpeg";
    private const string UploadFileName = "progress-photo.jpg";
    private const string UploadFormFileField = "file";
    private const string AmazonS3HostSuffix = ".amazonaws.com";
    private const string RegionalS3HostSegment = ".s3.";
    private const string UploadIntentMetadataField = "x-amz-meta-upload-intent-id";
    private const string ChecksumAlgorithmField = "x-amz-checksum-algorithm";
    private const string ChecksumSha256Field = "x-amz-checksum-sha256";
    private const string Sha256Algorithm = "SHA256";
    private const string SignatureAlgorithm = "AWS4-HMAC-SHA256";
    private const int MaximumPhotoBytes = 15 * 1024 * 1024;
    private const int HistoryPageSize = 100;
    private const int PhotoKeySegmentCount = 3;
    private const int PhotoPrefixSegmentIndex = 0;
    private const int PhotoOwnerSegmentIndex = 1;
    private const int PhotoIdentifierSegmentIndex = 2;
    private readonly Dictionary<Guid, int> _entryIds = [];
    private readonly Dictionary<int, Guid> _photoIds = [];
    private readonly object _identityLock = new();
    private int _nextEntryId;
    private long _sessionGeneration;

    public async Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        // This store can outlive a sign-in session. Retire old UI IDs before reading another account.
        long generation;
        lock (_identityLock)
        {
            generation = checked(++_sessionGeneration);
            _entryIds.Clear();
            _photoIds.Clear();
        }
        var pending = new List<(Guid PhotoId, PhotoVaultEntryDto Entry)>();
        var offset = 0;
        while (true)
        {
            var response = await apiClient.GetAsync<PhotoHistoryResponse>(
                $"{PhotosRoute}?limit={HistoryPageSize}&offset={offset}", cancellationToken).ConfigureAwait(false);
            ThrowIfSuperseded(generation);
            var history = RequireData(response, "photo history");
            if (history.Photos.Count == 0 && offset < history.TotalCount)
                throw new InvalidOperationException("Photo history pagination did not advance.");

            foreach (var photo in history.Photos)
            {
                var download = await apiClient.GetAsync<PhotoDownloadUrlResponse>(
                    $"{PhotosRoute}/{photo.Id:D}/download-url", cancellationToken).ConfigureAwait(false);
                ThrowIfSuperseded(generation);
                var url = download.Status == BackendApiStatus.Success &&
                          download.Data?.PhotoId == photo.Id &&
                          download.Data.ExpiresAtUtc > DateTime.UtcNow &&
                          IsApprovedStorageUrl(download.Data.PresignedDownloadUrl) &&
                          HasPresignedDownloadSignature(download.Data.PresignedDownloadUrl) &&
                          HasExactObjectPath(download.Data.PresignedDownloadUrl, photo.S3Key)
                    ? download.Data.PresignedDownloadUrl
                    : string.Empty;
                var phase = Enum.TryParse<CyclePhase>(photo.Phase, true, out var parsedPhase)
                    ? parsedPhase : CyclePhase.Unknown;
                pending.Add((photo.Id, new PhotoVaultEntryDto(
                    default, url, photo.Date.ToDateTime(TimeOnly.MinValue),
                    photo.WeightKg.HasValue ? (double)photo.WeightKg.Value : null,
                    photo.Notes, phase, url.Length > 0)));
            }

            offset += history.Photos.Count;
            if (offset >= history.TotalCount) break;
        }

        lock (_identityLock)
        {
            if (generation != _sessionGeneration) throw Superseded();
            var entries = new List<PhotoVaultEntryDto>(pending.Count);
            foreach (var (photoId, entry) in pending)
            {
                if (!_entryIds.TryGetValue(photoId, out var entryId))
                {
                    entryId = checked(++_nextEntryId);
                    _entryIds.Add(photoId, entryId);
                    _photoIds.Add(entryId, photoId);
                }
                entries.Add(entry with { Id = entryId });
            }
            return new PhotoVaultLoadResult(CyclePhase.Unknown, entries);
        }
    }

    public async Task<bool> AddAsync(AddPhotoVaultEntryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImagePath) || !File.Exists(request.ImagePath))
            throw new FileNotFoundException("The selected progress photo is unavailable.");
        var generation = CurrentGeneration();
        var declaredLength = new FileInfo(request.ImagePath).Length;
        if (declaredLength is 0 or > MaximumPhotoBytes)
            throw new InvalidDataException("The progress photo exceeds the allowed upload size.");

        var bytes = await File.ReadAllBytesAsync(request.ImagePath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (bytes.Length is 0 or > MaximumPhotoBytes)
                throw new InvalidDataException("The progress photo exceeds the allowed upload size.");
            ThrowIfSuperseded(generation);

            var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var intent = RequireData(await apiClient.SendMutationAsync<PhotoUploadUrlRequest, PhotoUploadIssueResponse>(
                HttpMethod.Post, $"{PhotosRoute}/upload-url",
                new PhotoUploadUrlRequest(JpegContentType, bytes.Length, IdempotencyKey: Guid.NewGuid().ToString("N"),
                    Sha256Checksum: checksum),
                cancellationToken: cancellationToken).ConfigureAwait(false), "photo upload intent");
            ThrowIfSuperseded(generation);
            if (intent.PhotoId == Guid.Empty || intent.UploadIntentId != intent.PhotoId ||
                !HasOwnerBoundPhotoKey(intent.S3Key, intent.PhotoId) || intent.ExpiresAtUtc <= DateTime.UtcNow ||
                !IsApprovedStorageUrl(intent.UploadUrl) || intent.Fields.Count == 0 ||
                !intent.Fields.TryGetValue("key", out var formKey) || formKey != intent.S3Key ||
                !intent.Fields.TryGetValue("Content-Type", out var formContentType) ||
                formContentType != JpegContentType ||
                !intent.Fields.TryGetValue(UploadIntentMetadataField, out var formIntentId) ||
                formIntentId != intent.UploadIntentId.ToString("D") ||
                !intent.Fields.TryGetValue(ChecksumAlgorithmField, out var checksumAlgorithm) ||
                checksumAlgorithm != Sha256Algorithm ||
                !intent.Fields.TryGetValue(ChecksumSha256Field, out var formChecksum) ||
                formChecksum != Convert.ToBase64String(Convert.FromHexString(checksum)) ||
                !intent.Fields.TryGetValue("x-amz-algorithm", out var signatureAlgorithm) ||
                signatureAlgorithm != SignatureAlgorithm ||
                !intent.Fields.ContainsKey("policy") || !intent.Fields.ContainsKey("x-amz-signature"))
                throw new InvalidOperationException("The photo upload intent is incomplete.");

            using var form = new MultipartFormDataContent();
            foreach (var field in intent.Fields)
                form.Add(new StringContent(field.Value), field.Key);
            using var image = new ByteArrayContent(bytes);
            image.Headers.ContentType = new MediaTypeHeaderValue(JpegContentType);
            form.Add(image, UploadFormFileField, UploadFileName);
            using var upload = await photoTransferClient.PostAsync(intent.UploadUrl, form, cancellationToken)
                .ConfigureAwait(false);
            ThrowIfSuperseded(generation);
            if (!upload.IsSuccessStatusCode)
                throw new InvalidOperationException("The progress photo upload failed.");

            ThrowIfSuperseded(generation);
            var committed = RequireData(await apiClient.SendMutationAsync<CommitPhotoMetadataRequest, CommitPhotoMetadataResponse>(
                HttpMethod.Post, $"{PhotosRoute}/commit",
                new CommitPhotoMetadataRequest(intent.PhotoId, DateOnly.FromDateTime(request.Date), intent.S3Key,
                    request.WeightKg.HasValue ? (decimal)request.WeightKg.Value : null,
                    request.Notes, request.Phase.ToString()),
                cancellationToken: cancellationToken).ConfigureAwait(false), "photo metadata commit");
            ThrowIfSuperseded(generation);
            if (!committed.Success || committed.PhotoId != intent.PhotoId)
                throw new InvalidOperationException("The server did not confirm the progress photo.");

            File.Delete(request.ImagePath);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken)
    {
        Guid photoId;
        long generation;
        lock (_identityLock)
        {
            if (!_photoIds.TryGetValue(entryId, out photoId)) return false;
            generation = _sessionGeneration;
        }

        var response = await apiClient.DeleteAsync($"{PhotosRoute}/{photoId:D}", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ThrowIfSuperseded(generation);
        if (response.Status == BackendApiStatus.NotFound) return false;
        if (response.Status != BackendApiStatus.Success || response.Data != true)
            throw new InvalidOperationException("The server did not delete the progress photo.");
        lock (_identityLock)
        {
            if (generation != _sessionGeneration) throw Superseded();
            _photoIds.Remove(entryId);
            _entryIds.Remove(photoId);
        }
        return true;
    }

    private long CurrentGeneration()
    {
        lock (_identityLock) return _sessionGeneration;
    }

    private void ThrowIfSuperseded(long generation)
    {
        lock (_identityLock)
            if (generation != _sessionGeneration) throw Superseded();
    }

    private static OperationCanceledException Superseded() =>
        new("The photo account session changed during this operation.");

    private static T RequireData<T>(BackendApiResponse<T> response, string operation) where T : class =>
        response.Status == BackendApiStatus.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"Cloud {operation} failed ({response.Status}).");

    private static bool IsApprovedStorageUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.Host.EndsWith(AmazonS3HostSuffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var s3Index = uri.Host.LastIndexOf(RegionalS3HostSegment, StringComparison.OrdinalIgnoreCase);
        if (s3Index <= 0) return false;
        var region = uri.Host.AsSpan(s3Index + RegionalS3HostSegment.Length,
            uri.Host.Length - AmazonS3HostSuffix.Length - s3Index - RegionalS3HostSegment.Length);
        return region.Length > 0 && region.IndexOf('.') < 0;
    }

    private static bool HasPresignedDownloadSignature(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Query.Contains("X-Amz-Algorithm=", StringComparison.Ordinal) &&
        uri.Query.Contains("X-Amz-Expires=", StringComparison.Ordinal) &&
        uri.Query.Contains("X-Amz-Signature=", StringComparison.Ordinal);

    private static bool HasExactObjectPath(string url, string key) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        string.Equals(Uri.UnescapeDataString(uri.AbsolutePath), "/" + key, StringComparison.Ordinal);

    private static bool HasOwnerBoundPhotoKey(string? key, Guid photoId)
    {
        if (key is null) return false;
        var parts = key.Split('/');
        return parts.Length == PhotoKeySegmentCount && parts[PhotoPrefixSegmentIndex] == "photos" &&
               Guid.TryParseExact(parts[PhotoOwnerSegmentIndex], "D", out var ownerId) && ownerId != Guid.Empty &&
               parts[PhotoIdentifierSegmentIndex] == photoId.ToString("N");
    }
}
