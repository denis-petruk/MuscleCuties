using System.Net;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Caching;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class BackendApiRefreshFailureTests
{
    private const string ApiBaseAddress = "https://example.test";
    private const string ProtectedRoute = "/api/v1/synthetic";
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private const string SyntheticAccessToken = "synthetic-access";
    private const string SyntheticRefreshToken = "synthetic-refresh";
    private static readonly BackendSessionTokens Tokens = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), SyntheticAccessToken,
        SyntheticRefreshToken, DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.InternalServerError, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.BadRequest, BackendApiStatus.ValidationFailed, false)]
    [InlineData(HttpStatusCode.Unauthorized, BackendApiStatus.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, BackendApiStatus.Unauthorized, true)]
    public async Task RefreshFailurePreservesOrClearsSessionAccordingToStatus(
        HttpStatusCode refreshStatus, BackendApiStatus expectedStatus, bool shouldClear)
    {
        using var handler = new RefreshFailureHandler(refreshStatus);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var tokenStore = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var client = new MuscleCutiesBackendApiClient(httpClient, tokenStore, cache, new OnlineAuthorityGate());

        var result = await client.GetAsync<object>(ProtectedRoute);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(shouldClear, tokenStore.Cleared);
        Assert.Equal(shouldClear, cache.Cleared);
        Assert.Equal(shouldClear ? null : Tokens, tokenStore.Tokens);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task MalformedRefreshResponsePreservesSessionAndReturnsServerError()
    {
        using var handler = new RefreshFaultHandler(malformed: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var tokenStore = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var client = new MuscleCutiesBackendApiClient(httpClient, tokenStore, cache, new OnlineAuthorityGate());

        var result = await client.GetAsync<object>(ProtectedRoute);

        Assert.Equal(BackendApiStatus.ServerError, result.Status);
        Assert.Equal(Tokens, tokenStore.Tokens);
        Assert.False(cache.Cleared);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshTransportFailurePreservesSession(bool timeout)
    {
        using var handler = new RefreshFaultHandler(malformed: false, timeout);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var tokenStore = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var client = new MuscleCutiesBackendApiClient(httpClient, tokenStore, cache, new OnlineAuthorityGate());

        if (timeout)
            await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetAsync<object>(ProtectedRoute));
        else
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync<object>(ProtectedRoute));

        Assert.Equal(Tokens, tokenStore.Tokens);
        Assert.False(cache.Cleared);
    }

    private sealed class RefreshFaultHandler(bool malformed, bool timeout = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath != RefreshRoute)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (malformed)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") });
            return timeout
                ? Task.FromException<HttpResponseMessage>(new TaskCanceledException())
                : Task.FromException<HttpResponseMessage>(new HttpRequestException());
        }
    }

    private sealed class RefreshFailureHandler(HttpStatusCode refreshStatus) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var status = request.RequestUri?.AbsolutePath == RefreshRoute ? refreshStatus : HttpStatusCode.Unauthorized;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class RecordingTokenStore(BackendSessionTokens tokens) : IBackendSessionTokenStore
    {
        public BackendSessionTokens? Tokens { get; private set; } = tokens;
        public bool Cleared { get; private set; }
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tokens);
        public Task WriteTokensAsync(BackendSessionTokens value, CancellationToken cancellationToken = default)
        {
            Tokens = value;
            return Task.CompletedTask;
        }
        public Task ClearTokensAsync(CancellationToken cancellationToken = default)
        {
            Cleared = true;
            Tokens = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCache : ILocalCacheService
    {
        public bool Cleared { get; private set; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Cleared = true; return Task.CompletedTask; }
        public Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveActiveWorkoutSnapshotAsync(Guid accountId, ActiveWorkoutSessionSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?>(null);
        public Task SaveCycleStateSnapshotAsync(Guid accountId, CycleStateSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<CachedSnapshotEnvelope<CycleStateSnapshot>?>(null);
        public Task SaveNutritionTargetsSnapshotAsync(Guid accountId, DailyNutritionTargetsSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?>(null);
    }

    private sealed class OnlineAuthorityGate : ICloudAuthorityGate
    {
        public int AuthorityVersion => 1;
        public CloudAuthorityMode CurrentMode => CloudAuthorityMode.ServerAuthoritativeCutover;
        public bool IsNetworkOnline => true;
        public void SetAuthorityMode(CloudAuthorityMode mode) { }
        public void SetNetworkOnline(bool isOnline) { }
        public OfflineWriteCheckResult EvaluateWriteAllowed(string operationName) => new(true, null, null);
    }
}
