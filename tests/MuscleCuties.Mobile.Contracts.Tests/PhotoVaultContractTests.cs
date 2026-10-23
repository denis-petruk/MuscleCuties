using System.Net;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class PhotoVaultContractTests
{
    private static readonly Guid PhotoId = Guid.Parse("e9680a15-f65d-41a7-899d-c58196561dc1");
    private static readonly Guid OwnerId = Guid.Parse("a88c49ac-5268-4d9e-aeb7-145175d071b9");
    private static readonly string PhotoKey = $"photos/{OwnerId:D}/{PhotoId:N}";
    private const string StorageUrl = "https://synthetic-bucket.s3.us-east-2.amazonaws.com/";
    private static readonly string SignedStorageUrl = StorageUrl + PhotoKey +
        "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=300&X-Amz-Signature=synthetic";
    private const long OversizedSparseFileLength = (long)int.MaxValue + 1;

    [Xunit.Fact]
    public async Task UploadCommitsOnlyAfterStorageAcceptsBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var api = new RecordingPhotoApi();
            var handler = new UploadHandler(HttpStatusCode.NoContent);
            var store = new MobilePhotoVaultStore(api, new HttpClient(handler));

            var added = await store.AddAsync(new AddPhotoVaultEntryRequest(
                path, new DateTime(2026, 10, 19), null, "Synthetic", MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown),
                CancellationToken.None);

            Xunit.Assert.True(added);
            Xunit.Assert.False(File.Exists(path));
            Xunit.Assert.Equal(["POST /api/v1/photos/upload-url", "POST /api/v1/photos/commit"], api.Calls);
            Xunit.Assert.Contains("filename=progress-photo.jpg", handler.UploadBody);
            Xunit.Assert.Contains("name=key", handler.UploadBody);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task RejectedStorageUploadNeverCommitsMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var api = new RecordingPhotoApi();
            var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.Forbidden)));

            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(
                new AddPhotoVaultEntryRequest(path, new DateTime(2026, 10, 19), null, "Synthetic",
                    MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown), CancellationToken.None));

            Xunit.Assert.Equal(["POST /api/v1/photos/upload-url"], api.Calls);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task NonS3UploadDestinationIsRejectedBeforePhotoTransfer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var api = new RecordingPhotoApi { UploadUrl = "https://example.com/upload" };
            var handler = new UploadHandler(HttpStatusCode.NoContent);
            var store = new MobilePhotoVaultStore(api, new HttpClient(handler));

            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(
                new AddPhotoVaultEntryRequest(path, new DateTime(2026, 10, 19), null, "Synthetic",
                    MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown), CancellationToken.None));

            Xunit.Assert.Equal(["POST /api/v1/photos/upload-url"], api.Calls);
            Xunit.Assert.Equal(string.Empty, handler.UploadBody);
            Xunit.Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task ListResolvesDownloadUrlAndDeletesGuidPhoto()
    {
        var api = new RecordingPhotoApi();
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));

        var loaded = await store.LoadAsync(CancellationToken.None);
        var entry = Xunit.Assert.Single(loaded.Photos);
        Xunit.Assert.True(entry.IsImageAvailable);
        Xunit.Assert.Equal(SignedStorageUrl, entry.ImagePath);
        Xunit.Assert.True(await store.DeleteAsync(entry.Id, CancellationToken.None));
        Xunit.Assert.Equal([
            "GET /api/v1/photos?limit=100&offset=0",
            $"GET /api/v1/photos/{PhotoId:D}/download-url",
            $"DELETE /api/v1/photos/{PhotoId:D}"
        ], api.Calls);
    }

    [Xunit.Fact]
    public async Task FailedHistoryReadDoesNotAppearAsEmptyVault()
    {
        var api = new RecordingPhotoApi { HistoryStatus = BackendApiStatus.ServerError };
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Xunit.Assert.Equal(["GET /api/v1/photos?limit=100&offset=0"], api.Calls);
    }

    [Xunit.Fact]
    public async Task ReadRejectsSignedUrlForAnotherObject()
    {
        var api = new RecordingPhotoApi { DownloadUrl = StorageUrl +
            "photos/other-owner/other-photo?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=300&X-Amz-Signature=synthetic" };
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));

        var photo = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);
        Xunit.Assert.False(photo.IsImageAvailable);
        Xunit.Assert.Equal(string.Empty, photo.ImagePath);
    }

    [Xunit.Fact]
    public async Task LoadingSecondAccountRetiresFirstAccountsDeleteIdentity()
    {
        var api = new RecordingPhotoApi();
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));
        var firstEntry = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);
        api.ActiveOwnerId = Guid.Parse("e1c82f08-c50e-4899-a5bd-cb1859cf147a");
        api.ActivePhotoId = Guid.Parse("ea42fd87-7dfe-4b2e-88ca-bba6e785582d");
        var secondEntry = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);

        Xunit.Assert.NotEqual(firstEntry.Id, secondEntry.Id);
        Xunit.Assert.False(await store.DeleteAsync(firstEntry.Id, CancellationToken.None));
        Xunit.Assert.True(await store.DeleteAsync(secondEntry.Id, CancellationToken.None));
        Xunit.Assert.DoesNotContain($"DELETE /api/v1/photos/{PhotoId:D}", api.Calls);
    }

    [Xunit.Fact]
    public async Task UploadRejectsMismatchedIntentChecksumBeforeStorageTransfer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var api = new RecordingPhotoApi { WrongChecksum = true };
            var handler = new UploadHandler(HttpStatusCode.NoContent);
            var store = new MobilePhotoVaultStore(api, new HttpClient(handler));
            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(
                new AddPhotoVaultEntryRequest(path, new DateTime(2026, 10, 19), null, "Synthetic",
                    MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown), CancellationToken.None));
            Xunit.Assert.Empty(handler.UploadBody);
            Xunit.Assert.Equal(["POST /api/v1/photos/upload-url"], api.Calls);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task OversizedPhotoIsRejectedBeforeReadOrApiCall()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                file.SetLength(OversizedSparseFileLength);
            var api = new RecordingPhotoApi();
            var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));

            await Xunit.Assert.ThrowsAsync<InvalidDataException>(() => store.AddAsync(
                new AddPhotoVaultEntryRequest(path, new DateTime(2026, 10, 19), null, "Synthetic",
                    MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown), CancellationToken.None));
            Xunit.Assert.Empty(api.Calls);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task LateFirstAccountLoadCannotPublishAfterSecondAccountLoad()
    {
        var api = new DelayedTwoAccountPhotoApi();
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));
        var firstLoad = store.LoadAsync(CancellationToken.None);
        await api.FirstHistoryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondEntry = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);
        api.ReleaseFirstHistory.TrySetResult();
        await Xunit.Assert.ThrowsAsync<OperationCanceledException>(() => firstLoad);

        Xunit.Assert.Contains(DelayedTwoAccountPhotoApi.SecondOwnerId.ToString("D"), secondEntry.ImagePath);
        Xunit.Assert.True(await store.DeleteAsync(secondEntry.Id, CancellationToken.None));
        Xunit.Assert.Equal($"/api/v1/photos/{DelayedTwoAccountPhotoApi.SecondPhotoId:D}", api.LastDeletedPath);
    }

    [Xunit.Fact]
    public async Task AccountChangeDuringUploadIntentPreventsPhotoTransferAndCommit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-photo-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var api = new RecordingPhotoApi { DelayUploadIntent = true };
            var handler = new UploadHandler(HttpStatusCode.NoContent);
            var store = new MobilePhotoVaultStore(api, new HttpClient(handler));
            var upload = store.AddAsync(new AddPhotoVaultEntryRequest(
                path, new DateTime(2026, 10, 19), null, "Synthetic",
                MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown), CancellationToken.None);
            await api.UploadIntentStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await store.LoadAsync(CancellationToken.None);
            api.ReleaseUploadIntent.TrySetResult();

            await Xunit.Assert.ThrowsAsync<OperationCanceledException>(() => upload);
            Xunit.Assert.Empty(handler.UploadBody);
            Xunit.Assert.DoesNotContain("POST /api/v1/photos/commit", api.Calls);
            Xunit.Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task LateFirstAccountDeleteCannotRemoveSecondAccountsPhotoIdentity()
    {
        var api = new RecordingPhotoApi { DelayFirstDelete = true };
        var store = new MobilePhotoVaultStore(api, new HttpClient(new UploadHandler(HttpStatusCode.NoContent)));
        var firstEntry = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);
        var firstDelete = store.DeleteAsync(firstEntry.Id, CancellationToken.None);
        await api.FirstDeleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        api.ActiveOwnerId = Guid.Parse("e1c82f08-c50e-4899-a5bd-cb1859cf147a");
        api.ActivePhotoId = Guid.Parse("ea42fd87-7dfe-4b2e-88ca-bba6e785582d");
        var secondEntry = Xunit.Assert.Single((await store.LoadAsync(CancellationToken.None)).Photos);
        api.ReleaseFirstDelete.TrySetResult();

        await Xunit.Assert.ThrowsAsync<OperationCanceledException>(() => firstDelete);
        Xunit.Assert.True(await store.DeleteAsync(secondEntry.Id, CancellationToken.None));
        Xunit.Assert.Contains($"DELETE /api/v1/photos/{api.ActivePhotoId:D}", api.Calls);
    }

    private sealed class UploadHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string UploadBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Xunit.Assert.Equal(HttpMethod.Post, request.Method);
            Xunit.Assert.Equal(StorageUrl, request.RequestUri?.ToString());
            UploadBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status);
        }
    }

    private sealed class RecordingPhotoApi : IMuscleCutiesBackendApiClient
    {
        public List<string> Calls { get; } = [];
        public string UploadUrl { get; init; } = StorageUrl;
        public bool WrongChecksum { get; init; }
        public bool DelayUploadIntent { get; init; }
        public bool DelayFirstDelete { get; init; }
        public TaskCompletionSource UploadIntentStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseUploadIntent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstDeleteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstDelete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _deleteCalls;
        public string? DownloadUrl { get; init; }
        public Guid ActivePhotoId { get; set; } = PhotoId;
        public Guid ActiveOwnerId { get; set; } = OwnerId;
        public BackendApiStatus HistoryStatus { get; init; } = BackendApiStatus.Success;

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string path, CancellationToken ct = default)
        {
            Calls.Add($"GET {path}");
            object data = path.EndsWith("/download-url", StringComparison.Ordinal)
                ? new PhotoDownloadUrlResponse(ActivePhotoId, DownloadUrl ?? StorageUrl + $"photos/{ActiveOwnerId:D}/{ActivePhotoId:N}" +
                    "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=300&X-Amz-Signature=synthetic", DateTime.UtcNow.AddMinutes(5))
                : new PhotoHistoryResponse([
                    new PhotoItemDto(ActivePhotoId, new DateOnly(2026, 10, 19), $"photos/{ActiveOwnerId:D}/{ActivePhotoId:N}", SignedStorageUrl,
                        null, "Synthetic", "Unknown", DateTime.UtcNow)
                ], 1);
            var status = path.EndsWith("/download-url", StringComparison.Ordinal)
                ? BackendApiStatus.Success : HistoryStatus;
            return Task.FromResult(new BackendApiResponse<TResponse>(status, (TResponse)data));
        }

        public async Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string path, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"{method.Method} {path}");
            if (DelayUploadIntent && path.EndsWith("/upload-url", StringComparison.Ordinal))
            {
                UploadIntentStarted.TrySetResult();
                await ReleaseUploadIntent.Task.WaitAsync(cancellationToken);
            }
            object data = path.EndsWith("/upload-url", StringComparison.Ordinal)
                ? new PhotoUploadIssueResponse(PhotoId, PhotoId, PhotoKey, UploadUrl,
                    new Dictionary<string, string> {
                        ["key"] = PhotoKey, ["Content-Type"] = "image/jpeg",
                        ["x-amz-meta-upload-intent-id"] = PhotoId.ToString("D"),
                        ["x-amz-checksum-algorithm"] = "SHA256",
                        ["x-amz-checksum-sha256"] = WrongChecksum ? "wrong" :
                            Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData([0xFF, 0xD8, 0xFF, 0xD9])),
                        ["x-amz-algorithm"] = "AWS4-HMAC-SHA256", ["policy"] = "synthetic-policy",
                        ["x-amz-signature"] = "synthetic-signature"
                    },
                    DateTime.UtcNow.AddMinutes(5))
                : new CommitPhotoMetadataResponse(true, PhotoId, "confirmed");
            return new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)data);
        }

        public async Task<BackendApiResponse<bool>> DeleteAsync(string path, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"DELETE {path}");
            if (DelayFirstDelete && Interlocked.Increment(ref _deleteCalls) == 1)
            {
                FirstDeleteStarted.TrySetResult();
                await ReleaseFirstDelete.Task.WaitAsync(cancellationToken);
            }
            return new BackendApiResponse<bool>(BackendApiStatus.Success, true);
        }
    }

    private sealed class DelayedTwoAccountPhotoApi : IMuscleCutiesBackendApiClient
    {
        public static readonly Guid SecondOwnerId = Guid.Parse("e1c82f08-c50e-4899-a5bd-cb1859cf147a");
        public static readonly Guid SecondPhotoId = Guid.Parse("ea42fd87-7dfe-4b2e-88ca-bba6e785582d");
        public TaskCompletionSource FirstHistoryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstHistory { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? LastDeletedPath { get; private set; }
        private int _historyCalls;

        public async Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string path, CancellationToken ct = default)
        {
            if (path.Contains("download-url", StringComparison.Ordinal))
            {
                var firstAccount = path.Contains(PhotoId.ToString("D"), StringComparison.Ordinal);
                var owner = firstAccount ? OwnerId : SecondOwnerId;
                var photo = firstAccount ? PhotoId : SecondPhotoId;
                var url = StorageUrl + $"photos/{owner:D}/{photo:N}" +
                          "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=300&X-Amz-Signature=synthetic";
                return new BackendApiResponse<TResponse>(BackendApiStatus.Success,
                    (TResponse)(object)new PhotoDownloadUrlResponse(photo, url, DateTime.UtcNow.AddMinutes(5)));
            }

            var first = Interlocked.Increment(ref _historyCalls) == 1;
            if (first)
            {
                FirstHistoryStarted.TrySetResult();
                await ReleaseFirstHistory.Task.WaitAsync(ct);
            }
            var historyPhoto = first ? PhotoId : SecondPhotoId;
            var historyOwner = first ? OwnerId : SecondOwnerId;
            var history = new PhotoHistoryResponse([
                new PhotoItemDto(historyPhoto, new DateOnly(2026, 10, 19),
                    $"photos/{historyOwner:D}/{historyPhoto:N}", string.Empty,
                    null, "Synthetic", "Unknown", DateTime.UtcNow)
            ], 1);
            return new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)history);
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string path, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<bool>> DeleteAsync(string path, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            LastDeletedPath = path;
            return Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
        }
    }
}
