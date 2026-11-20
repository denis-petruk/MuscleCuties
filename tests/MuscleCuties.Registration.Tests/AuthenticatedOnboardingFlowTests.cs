using System.Text.Json;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class AuthenticatedOnboardingFlowTests
{
    private const string OnboardingCompleteRoute = "/api/v1/accounts/onboarding/complete";

    [Fact]
    public async Task CloudAuthServiceReportsIncompleteOnboardingFromStoredSessionTokens()
    {
        var accountId = Guid.NewGuid();
        var tokenStore = new InMemoryTokenStore(new BackendSessionTokens(
            accountId,
            "synthetic-access",
            "synthetic-refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            IsOnboardingComplete: false));
        var gateway = new MobileJourneyCloudGateway(
            new RecordingApiClient(BackendApiStatus.Success),
            tokenStore,
            new NullCacheService(),
            TimeProvider.System);
        var authService = new CloudAuthService(tokenStore, gateway, new RecordingApiClient(BackendApiStatus.Success));

        var state = await authService.GetCurrentUserStateAsync();

        Assert.NotNull(state);
        Assert.False(state!.IsOnboardingComplete);
    }

    [Fact]
    public async Task AuthenticatedIncompleteAccountCanStageProfileAndCompleteOnboardingQuiz()
    {
        var accountId = Guid.NewGuid();
        var tokenStore = new InMemoryTokenStore(new BackendSessionTokens(
            accountId,
            "synthetic-access",
            "synthetic-refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            IsOnboardingComplete: false));
        var pending = new PendingRegistrationService();
        var apiClient = new RecordingApiClient(BackendApiStatus.Success);
        var gateway = new MobileJourneyCloudGateway(apiClient, tokenStore, new NullCacheService(), TimeProvider.System);
        var profileSetupStore = new CloudProfileSetupStore(pending, tokenStore);
        var quizStore = new CloudOnboardingQuizStore(pending, gateway, apiClient, tokenStore);

        var initialLoad = await profileSetupStore.LoadAsync(CancellationToken.None);
        Assert.True(initialLoad.HasPendingRegistration);
        Assert.Null(initialLoad.Profile);

        var draft = new ProfileSetupDraft(
            "Synthetic Apple Member",
            new DateTime(1996, 4, 12),
            168f,
            62f,
            string.Empty,
            28,
            [(int)WorkoutActivityType.HighVolumeStrength],
            UseMetricSystem: true);
        var saved = await profileSetupStore.SaveAndPrepareQuizAsync(draft, CancellationToken.None);
        Assert.True(saved);

        var reloaded = await profileSetupStore.LoadAsync(CancellationToken.None);
        Assert.True(reloaded.HasPendingRegistration);
        Assert.NotNull(reloaded.Profile);
        Assert.Equal("Synthetic Apple Member", reloaded.Profile!.Name);

        var commit = await quizStore.CommitAsync([new QuizSelectionDto(1, 2)], CancellationToken.None);

        Assert.True(commit.Succeeded);
        Assert.False(commit.AccountCreatedNeedsLogin);
        Assert.Equal(OnboardingCompleteRoute, apiClient.LastMutationRoute);
        Assert.True((await tokenStore.ReadTokensAsync())!.IsOnboardingComplete);
        Assert.Null(pending.StagedProfile);
    }

    [Fact]
    public async Task AuthenticatedOnboardingFailurePreservesIncompleteSessionForRetry()
    {
        var accountId = Guid.NewGuid();
        var tokenStore = new InMemoryTokenStore(new BackendSessionTokens(
            accountId,
            "synthetic-access",
            "synthetic-refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            IsOnboardingComplete: false));
        var pending = new PendingRegistrationService();
        var apiClient = new RecordingApiClient(BackendApiStatus.ValidationFailed, "InvalidQuiz");
        var gateway = new MobileJourneyCloudGateway(apiClient, tokenStore, new NullCacheService(), TimeProvider.System);
        var profileSetupStore = new CloudProfileSetupStore(pending, tokenStore);
        var quizStore = new CloudOnboardingQuizStore(pending, gateway, apiClient, tokenStore);

        await profileSetupStore.SaveAndPrepareQuizAsync(new ProfileSetupDraft(
            "Synthetic Apple Member",
            new DateTime(1996, 4, 12),
            168f,
            62f,
            string.Empty,
            28,
            [(int)WorkoutActivityType.HighVolumeStrength],
            UseMetricSystem: true), CancellationToken.None);

        var commit = await quizStore.CommitAsync([new QuizSelectionDto(1, 2)], CancellationToken.None);

        Assert.False(commit.Succeeded);
        Assert.False(commit.AccountCreatedNeedsLogin);
        Assert.False((await tokenStore.ReadTokensAsync())!.IsOnboardingComplete);
        Assert.NotNull(pending.StagedProfile);
    }

    private sealed class InMemoryTokenStore(BackendSessionTokens? initial) : IBackendSessionTokenStore
    {
        private BackendSessionTokens? _tokens = initial;

        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens);

        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default)
        {
            _tokens = tokens;
            return Task.CompletedTask;
        }

        public Task ClearTokensAsync(CancellationToken cancellationToken = default)
        {
            _tokens = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingApiClient(BackendApiStatus status, string? errorCode = null)
        : IMuscleCutiesBackendApiClient
    {
        public string? LastMutationRoute { get; private set; }

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
            LastMutationRoute = relativePath;
            using var document = JsonDocument.Parse("""{"onboardingComplete":true}""");
            var response = new BackendApiResponse<JsonElement>(status, document.RootElement.Clone(), errorCode);
            return Task.FromResult((BackendApiResponse<TResponse>)(object)response);
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
