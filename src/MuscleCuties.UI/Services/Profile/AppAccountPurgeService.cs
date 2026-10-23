using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.UI.Services.Profile;

public sealed class AppAccountPurgeService : IAppAccountPurgeService
{
    private const string DeleteAccountRoute = "/api/v1/accounts/me";
    private const string AccountDeletionFailedMessage = "The server did not confirm account deletion.";
    private readonly Lazy<IAppPreloadService> _preloadService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccountSessionGate _sessionGate;
    private readonly ILocalPhotoCacheService _photoCache;
    private readonly ConcurrentDictionary<int, bool> _acknowledgedServerDeletions = new();

    public AppAccountPurgeService(
        IServiceScopeFactory scopeFactory,
        Lazy<IAppPreloadService> preloadService,
        AccountSessionGate sessionGate,
        ILocalPhotoCacheService photoCache)
    {
        _scopeFactory = scopeFactory;
        _preloadService = preloadService;
        _sessionGate = sessionGate;
        _photoCache = photoCache;
    }

    public async Task<AccountPurgeResult> PurgeAccountDataAsync(int userId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMuscleCutiesBackendApiClient>();
        var cloudGateway = scope.ServiceProvider.GetRequiredService<MobileJourneyCloudGateway>();

        ct.ThrowIfCancellationRequested();
        if (userId <= 0 || await authService.GetCurrentUserIdAsync() != userId)
            return new AccountPurgeResult(false, false, false,
                ["The active account changed before local deletion."]);

        using (await _sessionGate.EnterAsync(ct).ConfigureAwait(false))
        {
            var serverConfirmed = _acknowledgedServerDeletions.ContainsKey(userId);
            if (!serverConfirmed)
            {
                var deletion = await apiClient.SendMutationAsync<object, AccountDeletionResult>(
                    HttpMethod.Delete, DeleteAccountRoute, new { }, cancellationToken: ct).ConfigureAwait(false);
                if (deletion.Status != BackendApiStatus.Success || deletion.Data?.Succeeded != true)
                    return new AccountPurgeResult(false, false, false, [AccountDeletionFailedMessage]);

                _acknowledgedServerDeletions[userId] = true;
            }

            await cloudGateway.LogoutAndPurgeLocalStateAsync(ct).ConfigureAwait(false);

            await _photoCache.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            await authService.LogoutAsync().ConfigureAwait(false);
            _acknowledgedServerDeletions.TryRemove(userId, out _);
            _preloadService.Value.InvalidateAll();

            return new AccountPurgeResult(true, true, true, []);
        }
    }

    public async Task PurgeAllAccountDataAndKeysAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var currentUserId = await authService.GetCurrentUserIdAsync();
        if (currentUserId > 0)
        {
            var result = await PurgeAccountDataAsync(currentUserId);
            if (!result.Succeeded)
                throw new InvalidOperationException("Local account deletion is incomplete.");
        }
        else
        {
            await _photoCache.ClearAsync().ConfigureAwait(false);
            var cloudGateway = scope.ServiceProvider.GetService<MobileJourneyCloudGateway>();
            if (cloudGateway is not null)
            {
                await cloudGateway.LogoutAndPurgeLocalStateAsync().ConfigureAwait(false);
            }
            await authService.LogoutAsync().ConfigureAwait(false);
            _preloadService.Value.InvalidateAll();
        }
    }
}
