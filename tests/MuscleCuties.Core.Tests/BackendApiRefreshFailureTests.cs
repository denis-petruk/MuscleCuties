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
    private const string RotatedAccessToken = "synthetic-rotated-access";
    private const string RotatedRefreshToken = "synthetic-rotated-refresh";
    private static readonly BackendSessionTokens Tokens = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), SyntheticAccessToken,
        SyntheticRefreshToken, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task ConcurrentClientsShareOneRefreshAndRetainServerExpiration()
    {
        using var handler = new ConcurrentRefreshHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var tokenStore = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var first = new MuscleCutiesBackendApiClient(httpClient, tokenStore, cache, new OnlineAuthorityGate());
        var second = new MuscleCutiesBackendApiClient(httpClient, tokenStore, cache, new OnlineAuthorityGate());

        var results = await Task.WhenAll(first.GetAsync<object>(ProtectedRoute), second.GetAsync<object>(ProtectedRoute));

        Assert.All(results, result => Assert.Equal(BackendApiStatus.Success, result.Status));
        Assert.Equal(1, handler.RefreshCount);
        Assert.Equal(RotatedRefreshToken, tokenStore.Tokens?.RefreshToken);
        Assert.Equal(ConcurrentRefreshHandler.Expiration, tokenStore.Tokens?.AccessTokenExpiresAtUtc);
        Assert.False(tokenStore.Cleared);
        Assert.False(cache.Cleared);
    }

    [Fact]
    public async Task LogoutDuringRefreshDoesNotRestoreSession()
    {
        using var handler = new PausedRefreshHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var store = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var client = new MuscleCutiesBackendApiClient(httpClient, store, cache, new OnlineAuthorityGate());
        var gateway = new MobileJourneyCloudGateway(client, store, cache, TimeProvider.System);

        var request = client.GetAsync<object>(ProtectedRoute);
        await handler.RefreshStarted.Task;
        var logout = gateway.LogoutAndPurgeLocalStateAsync();
        handler.FinishRefresh.TrySetResult();
        await Task.WhenAll(request, logout);

        Assert.Null(store.Tokens);
        Assert.True(cache.Cleared);
    }

    [Fact]
    public async Task LogoutDoesNotClearSubsequentLoginForSameAccount()
    {
        using var handler = new PausedLogoutHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseAddress) };
        var store = new RecordingTokenStore(Tokens);
        var cache = new RecordingCache();
        var client = new MuscleCutiesBackendApiClient(httpClient, store, cache, new OnlineAuthorityGate());
        var gateway = new MobileJourneyCloudGateway(client, store, cache, TimeProvider.System);

        var logout = gateway.LogoutAndPurgeLocalStateAsync();
        await handler.LogoutStarted.Task;
        var login = await gateway.SignInWithPasswordAsync("synthetic", "synthetic");
        handler.FinishLogout.TrySetResult();
        await logout;

        Assert.Equal(BackendApiStatus.Success, login.Status);
        Assert.Equal(RotatedRefreshToken, store.Tokens?.RefreshToken);
        Assert.False(cache.Cleared);
    }

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

    private static HttpResponseMessage RotatedSessionResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent($$"""{"userId":"{{Tokens.AccountId}}","accessToken":"{{RotatedAccessToken}}","refreshToken":"{{RotatedRefreshToken}}","accessExpiresAtUtc":"{{ConcurrentRefreshHandler.Expiration:O}}"}""")
    };

    private sealed class PausedRefreshHandler : HttpMessageHandler
    {
        public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinishRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == RefreshRoute)
            {
                RefreshStarted.TrySetResult();
                await FinishRefresh.Task.WaitAsync(cancellationToken);
                return RotatedSessionResponse();
            }
            return new HttpResponseMessage(request.RequestUri?.AbsolutePath == ProtectedRoute
                ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent);
        }
    }

    private sealed class PausedLogoutHandler : HttpMessageHandler
    {
        public TaskCompletionSource LogoutStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinishLogout { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/api/v1/auth/revoke")
            {
                LogoutStarted.TrySetResult();
                await FinishLogout.Task.WaitAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return RotatedSessionResponse();
        }
    }

    private sealed class ConcurrentRefreshHandler : HttpMessageHandler
    {
        public static readonly DateTimeOffset Expiration = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        private readonly TaskCompletionSource bothInitialRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int initialCount;
        private int refreshCount;
        public int RefreshCount => refreshCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == RefreshRoute)
            {
                Interlocked.Increment(ref refreshCount);
                return RotatedSessionResponse();
            }

            if (request.Headers.Authorization?.Parameter == SyntheticAccessToken)
            {
                if (Interlocked.Increment(ref initialCount) == 2)
                    bothInitialRequests.TrySetResult();
                await bothInitialRequests.Task.WaitAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
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
