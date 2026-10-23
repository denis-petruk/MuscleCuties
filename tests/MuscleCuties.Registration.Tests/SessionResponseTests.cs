using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.Registration.Tests;

public sealed class SessionResponseTests
{
    [Xunit.Fact]
    public async Task MalformedSuccessDoesNotCreateAnAccountIdOrStoreTokens()
    {
        var tokens = new RecordingTokenStore();
        var cache = new RecordingCache();
        var gateway = new MobileJourneyCloudGateway(
            new StubApiClient("""{"accessToken":"token","refreshToken":"refresh"}"""),
            tokens, cache, TimeProvider.System);

        var response = await gateway.SignInWithPasswordAsync("synthetic@example.test", "synthetic-password");

        Xunit.Assert.Equal(BackendApiStatus.ServerError, response.Status);
        Xunit.Assert.Null(response.Data);
        Xunit.Assert.Null(tokens.Written);
        Xunit.Assert.False(cache.Activated);
    }

    [Xunit.Fact]
    public async Task CompleteSessionResponseStoresServerIssuedIdentity()
    {
        var accountId = Guid.NewGuid();
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var tokens = new RecordingTokenStore();
        var cache = new RecordingCache();
        var gateway = new MobileJourneyCloudGateway(
            new StubApiClient(JsonSerializer.Serialize(new
            {
                userId = accountId,
                accessToken = "synthetic-access",
                refreshToken = "synthetic-refresh",
                accessExpiresAtUtc = expiration
            })), tokens, cache, TimeProvider.System);

        var response = await gateway.SignInWithPasswordAsync("synthetic@example.test", "synthetic-password");

        Xunit.Assert.Equal(BackendApiStatus.Success, response.Status);
        Xunit.Assert.Equal(accountId, tokens.Written?.AccountId);
        Xunit.Assert.True(cache.Activated);
    }

    [Xunit.Fact]
    public async Task AppleSessionUsesServerRouteAndStoresOnlyCompleteTokens()
    {
        var accountId = Guid.NewGuid();
        var nonceChallengeId = Guid.NewGuid().ToString("D");
        var api = new StubApiClient(JsonSerializer.Serialize(new
        {
            userId = accountId,
            accessToken = "synthetic-access",
            refreshToken = "synthetic-refresh",
            accessExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        }));
        var tokens = new RecordingTokenStore();
        var cache = new RecordingCache();
        var gateway = new MobileJourneyCloudGateway(api, tokens, cache, TimeProvider.System);

        var response = await gateway.SignInWithAppleAsync("synthetic-identity", "synthetic-nonce", nonceChallengeId);

        Xunit.Assert.Equal(BackendApiStatus.Success, response.Status);
        Xunit.Assert.Equal("/api/v1/auth/apple", api.LastRoute);
        Xunit.Assert.Equal(accountId, tokens.Written?.AccountId);
        Xunit.Assert.True(cache.Activated);
    }

    [Xunit.Fact]
    public async Task AppleSessionWithoutNonceDoesNotCallServerOrStoreTokens()
    {
        var api = new StubApiClient("{}");
        var tokens = new RecordingTokenStore();
        var cache = new RecordingCache();
        var gateway = new MobileJourneyCloudGateway(api, tokens, cache, TimeProvider.System);

        var response = await gateway.SignInWithAppleAsync("synthetic-identity", string.Empty, string.Empty);

        Xunit.Assert.Equal(BackendApiStatus.ValidationFailed, response.Status);
        Xunit.Assert.Null(api.LastRoute);
        Xunit.Assert.Null(tokens.Written);
        Xunit.Assert.False(cache.Activated);
    }

    private sealed class StubApiClient(string responseJson) : IMuscleCutiesBackendApiClient
    {
        public string? LastRoute { get; private set; }
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            LastRoute = relativePath;
            var document = JsonDocument.Parse(responseJson);
            var response = new BackendApiResponse<JsonElement>(BackendApiStatus.Success,
                document.RootElement.Clone());
            document.Dispose();
            return Task.FromResult((BackendApiResponse<TResponse>)(object)response);
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath,
            string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTokenStore : IBackendSessionTokenStore
    {
        public BackendSessionTokens? Written { get; private set; }

        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Written);

        public Task WriteTokensAsync(BackendSessionTokens tokens,
            CancellationToken cancellationToken = default)
        {
            Written = tokens;
            return Task.CompletedTask;
        }

        public Task ClearTokensAsync(CancellationToken cancellationToken = default)
        {
            Written = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCache : ILocalCacheService
    {
        public bool Activated { get; private set; }

        public Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            Activated = true;
            return Task.CompletedTask;
        }

        public Task SaveActiveWorkoutSnapshotAsync(Guid accountId,
            ActiveWorkoutSessionSnapshot snapshot, DateTimeOffset capturedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveCycleStateSnapshotAsync(Guid accountId, CycleStateSnapshot snapshot,
            DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveNutritionTargetsSnapshotAsync(Guid accountId,
            DailyNutritionTargetsSnapshot snapshot, DateTimeOffset capturedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
