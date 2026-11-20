using System.Text.Json;
using MuscleCuties.Infrastructure.Account.SignIn;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Caching;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class CloudAccountSignInStoreTests
{
    [Fact]
    public async Task SignInWithPasswordReturnsNullWhenServerRejectsCredentials()
    {
        var store = CreateStore(BackendApiStatus.Unauthorized);

        var account = await store.SignInWithPasswordAsync("synthetic@example.invalid", "wrong-password");

        Assert.Null(account);
    }

    [Theory]
    [InlineData(BackendApiStatus.OfflineRejected)]
    [InlineData(BackendApiStatus.Timeout)]
    [InlineData(BackendApiStatus.ServerError)]
    [InlineData(BackendApiStatus.RateLimited)]
    public async Task SignInWithPasswordThrowsWhenBackendFailsInsteadOfReportingInvalidCredentials(
        BackendApiStatus failureStatus)
    {
        var store = CreateStore(failureStatus);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            store.SignInWithPasswordAsync("synthetic@example.invalid", "valid-password"));
    }

    [Theory]
    [InlineData(BackendApiStatus.OfflineRejected)]
    [InlineData(BackendApiStatus.Timeout)]
    [InlineData(BackendApiStatus.ServerError)]
    [InlineData(BackendApiStatus.RateLimited)]
    public async Task SignInWithPlatformReportsConnectionFailureWhenBackendIsUnavailable(
        BackendApiStatus failureStatus)
    {
        var store = CreateStore(failureStatus);

        var outcome = await store.SignInWithPlatformAsync(
            "apple",
            "synthetic-apple-user",
            null,
            null,
            "synthetic-identity",
            "synthetic-nonce",
            Guid.NewGuid().ToString("D"),
            authorizationCode: "synthetic-auth-code");

        Assert.False(outcome.Succeeded);
        Assert.Contains("connection", outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task SignInWithPlatformPropagatesServerOnboardingCompletionState(
        bool serverOnboardingComplete,
        bool expectedPendingOnboarding,
        bool expectedOnboardingComplete)
    {
        var payload = JsonSerializer.Serialize(new
        {
            userId = Guid.NewGuid(),
            accessToken = "synthetic-access",
            refreshToken = "synthetic-refresh",
            accessExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            isOnboardingComplete = serverOnboardingComplete
        });
        var gateway = new MobileJourneyCloudGateway(
            new JsonApiClient(payload),
            new NullTokenStore(),
            new NullCacheService(),
            TimeProvider.System);
        var store = new CloudAccountSignInStore(gateway);

        var outcome = await store.SignInWithPlatformAsync(
            "apple",
            "synthetic-apple-user",
            null,
            null,
            "synthetic-identity",
            "synthetic-nonce",
            Guid.NewGuid().ToString("D"),
            authorizationCode: "synthetic-auth-code");

        Assert.True(outcome.Succeeded);
        Assert.Equal(expectedPendingOnboarding, outcome.IsPendingOnboarding);
        Assert.Equal(expectedOnboardingComplete, outcome.IsOnboardingComplete);
    }

    private static CloudAccountSignInStore CreateStore(BackendApiStatus status)
    {
        var gateway = new MobileJourneyCloudGateway(
            new StatusApiClient(status),
            new NullTokenStore(),
            new NullCacheService(),
            TimeProvider.System);
        return new CloudAccountSignInStore(gateway);
    }

    private sealed class JsonApiClient(string responseJson) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            using var document = JsonDocument.Parse(responseJson);
            var response = new BackendApiResponse<JsonElement>(
                BackendApiStatus.Success,
                document.RootElement.Clone());
            return Task.FromResult((BackendApiResponse<TResponse>)(object)response);
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StatusApiClient(BackendApiStatus status) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var response = new BackendApiResponse<JsonElement>(status, default);
            return Task.FromResult((BackendApiResponse<TResponse>)(object)response);
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NullTokenStore : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendSessionTokens?>(null);

        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearTokensAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullCacheService : ILocalCacheService
    {
        public Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveActiveWorkoutSnapshotAsync(Guid accountId, ActiveWorkoutSessionSnapshot snapshot,
            DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveCycleStateSnapshotAsync(Guid accountId, CycleStateSnapshot snapshot,
            DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveNutritionTargetsSnapshotAsync(Guid accountId, DailyNutritionTargetsSnapshot snapshot,
            DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(
            Guid accountId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
