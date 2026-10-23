using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class AccountDeletionContractTests
{
    private const int ActiveUserId = 27;
    private const string AccountDeletionRoute = "/api/v1/accounts/me";

    [Xunit.Theory]
    [Xunit.InlineData(BackendApiStatus.NotFound, false)]
    [Xunit.InlineData(BackendApiStatus.Success, false)]
    public async Task FailedServerDeletionKeepsLocalAccountAndPhotoCache(
        BackendApiStatus status, bool serverSucceeded)
    {
        var api = new RecordingApiClient(status, serverSucceeded);
        var auth = new TestAuthService();
        var photoCache = new TestPhotoCache();
        using var provider = new ServiceCollection()
            .AddSingleton<IAuthService>(auth)
            .AddSingleton<IMuscleCutiesBackendApiClient>(api)
            .AddSingleton(new MobileJourneyCloudGateway(api, null!, null!, TimeProvider.System))
            .BuildServiceProvider();
        var purge = new AppAccountPurgeService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Lazy<MuscleCuties.Core.Services.IAppPreloadService>(() => throw new InvalidOperationException()),
            new AccountSessionGate(),
            photoCache);

        var result = await purge.PurgeAccountDataAsync(ActiveUserId);

        Xunit.Assert.False(result.Succeeded);
        Xunit.Assert.Equal((HttpMethod.Delete, AccountDeletionRoute), api.Mutation);
        Xunit.Assert.False(auth.LoggedOut);
        Xunit.Assert.False(photoCache.Cleared);
    }

    [Xunit.Fact]
    public async Task RetryAfterAcknowledgedServerDeletionCompletesLocalPurgeWithoutSecondDelete()
    {
        var api = new RecordingApiClient(BackendApiStatus.Success, true);
        var auth = new TestAuthService();
        var photoCache = new TestPhotoCache { FailOnce = true };
        var preload = new TestPreloadService();
        var tokenStore = new TestSessionTokenStore();
        var snapshotCache = new TestLocalCacheService();
        using var provider = new ServiceCollection()
            .AddSingleton<IAuthService>(auth)
            .AddSingleton<IMuscleCutiesBackendApiClient>(api)
            .AddSingleton(new MobileJourneyCloudGateway(api, tokenStore, snapshotCache, TimeProvider.System))
            .BuildServiceProvider();
        var purge = new AppAccountPurgeService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Lazy<MuscleCuties.Core.Services.IAppPreloadService>(() => preload),
            new AccountSessionGate(),
            photoCache);

        await Xunit.Assert.ThrowsAsync<IOException>(() => purge.PurgeAccountDataAsync(ActiveUserId));
        Xunit.Assert.Equal(1, api.CallCount);

        api.Status = BackendApiStatus.NotFound;
        api.Succeeded = false;

        var retryResult = await purge.PurgeAccountDataAsync(ActiveUserId);

        Xunit.Assert.True(retryResult.Succeeded);
        Xunit.Assert.Equal(1, api.CallCount);
        Xunit.Assert.True(photoCache.Cleared);
        Xunit.Assert.True(auth.LoggedOut);
        Xunit.Assert.True(preload.Invalidated);
    }

    private sealed class RecordingApiClient(BackendApiStatus status, bool succeeded) : IMuscleCutiesBackendApiClient
    {
        public BackendApiStatus Status { get; set; } = status;
        public bool Succeeded { get; set; } = succeeded;
        public int CallCount { get; private set; }
        public (HttpMethod Method, string Path)? Mutation { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string path, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            if (path == AccountDeletionRoute)
            {
                CallCount++;
                Mutation = (method, path);
                var data = new AccountDeletionResult(Succeeded, 0, [], string.Empty);
                return Task.FromResult(new BackendApiResponse<TResponse>(Status, (TResponse)(object)data));
            }

            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string path, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestPhotoCache : ILocalPhotoCacheService
    {
        public bool FailOnce { get; set; }
        public bool Cleared { get; private set; }
        public Task ActivateAccountAsync(Guid accountId, CancellationToken ct = default) => Task.CompletedTask;
        public Task StoreAsync(Guid accountId, Guid photoId, Stream source, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream?> OpenReadAsync(Guid accountId, Guid photoId, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
        public Task ClearAsync(CancellationToken ct = default)
        {
            if (FailOnce)
            {
                FailOnce = false;
                throw new IOException("Simulated photo cache removal failure.");
            }
            Cleared = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestPreloadService : MuscleCuties.Core.Services.IAppPreloadService
    {
        public bool Invalidated { get; private set; }
        public DateTime LastLoadedDate => DateTime.UtcNow;
        public Task PreloadAllAsync() => Task.CompletedTask;
        public Task PreloadDashboardAsync() => Task.CompletedTask;
        public Task PreloadRemainingAsync() => Task.CompletedTask;
        public Task RefreshAllAsync() => Task.CompletedTask;
        public void InvalidateDashboard() => Invalidated = true;
        public void InvalidateWorkout() => Invalidated = true;
        public void InvalidateAll() => Invalidated = true;
    }

    private sealed class TestSessionTokenStore : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken ct = default) => Task.FromResult<BackendSessionTokens?>(null);
        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearTokensAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestLocalCacheService : MuscleCuties.UseCases.Features.Caching.ILocalCacheService
    {
        public Task ActivateAccountAsync(Guid accountId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveActiveWorkoutSnapshotAsync(Guid accountId, MuscleCuties.UseCases.Features.Caching.ActiveWorkoutSessionSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(Guid accountId, CancellationToken ct = default) => Task.FromResult<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.ActiveWorkoutSessionSnapshot>?>(null);
        public Task SaveCycleStateSnapshotAsync(Guid accountId, MuscleCuties.UseCases.Features.Caching.CycleStateSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(Guid accountId, CancellationToken ct = default) => Task.FromResult<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.CycleStateSnapshot>?>(null);
        public Task SaveNutritionTargetsSnapshotAsync(Guid accountId, MuscleCuties.UseCases.Features.Caching.DailyNutritionTargetsSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(Guid accountId, CancellationToken ct = default) => Task.FromResult<MuscleCuties.UseCases.Features.Caching.CachedSnapshotEnvelope<MuscleCuties.UseCases.Features.Caching.DailyNutritionTargetsSnapshot>?>(null);
        public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestAuthService : IAuthService
    {
        public bool LoggedOut { get; private set; }
        public Task<User?> LoginAsync(string emailOrUsername, string password) => Task.FromResult<User?>(null);
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);
        public Task<bool> UsernameExistsAsync(string username) => Task.FromResult(false);
        public Task<User?> SignInWithAppleAsync(AppleSignInResult account) => Task.FromResult<User?>(null);
        public Task<AppleSignInOutcome> HandleAppleSignInAsync(AppleSignInResult account) => Task.FromResult(new AppleSignInOutcome(false, false, null, false));
        public Task<User?> SignInWithExternalProviderAsync(PlatformSignInResult account) => Task.FromResult<User?>(null);
        public Task<PasswordOperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword) => Task.FromResult(new PasswordOperationResult(false));
        public Task LogoutAsync()
        {
            LoggedOut = true;
            return Task.CompletedTask;
        }
        public Task<bool> IsLoggedInAsync() => Task.FromResult(true);
        public Task<int> GetCurrentUserIdAsync() => Task.FromResult(ActiveUserId);
        public Task<CurrentUserState?> GetCurrentUserStateAsync() => Task.FromResult<CurrentUserState?>(new CurrentUserState(ActiveUserId, true));
        public Task SetCurrentUserAsync(User user) => Task.CompletedTask;
    }
}
