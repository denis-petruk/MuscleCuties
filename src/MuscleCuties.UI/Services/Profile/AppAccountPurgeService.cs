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
    private const string AccountDeletionOfflineMessage =
        "Connect to the internet, then try account deletion again. Your account was not deleted.";
    private const string AccountDeletionSignInMessage =
        "Your sign-in has expired. Sign in again, then retry account deletion.";
    private const string AccountDeletionAppleRevocationMessage =
        "Apple could not verify sign-in revocation. Sign in with Apple again, then retry deletion.";
    private const string AccountDeletionAppleAuthorizationMessage =
        "Continue with Sign in with Apple to verify this deletion. Your account has not been deleted yet.";
    private const string AccountDeletionAppleAuthorizationCanceledMessage =
        "Sign in with Apple was not completed. Your account has not been deleted.";
    private const string AccountDeletionAppleAuthorizationFailedMessage =
        "Apple could not verify your sign-in. Your account has not been deleted. Try again.";
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
                var deletion = await RequestServerDeletionAsync(apiClient, ct).ConfigureAwait(false);
                if (deletion.Data?.FailureCode == AccountDeletionFailureCode.AppleAuthorizationRequired)
                {
                    var appleSignIn = scope.ServiceProvider.GetService<IAppleSignInService>();
                    if (appleSignIn is null)
                        return Failed(AccountDeletionAppleAuthorizationMessage);

                    AppleSignInResult? credential;
                    try
                    {
                        credential = await appleSignIn.SignInAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        return Failed(AccountDeletionAppleAuthorizationFailedMessage);
                    }

                    if (string.IsNullOrWhiteSpace(credential?.AuthorizationCode))
                        return Failed(AccountDeletionAppleAuthorizationCanceledMessage);

                    deletion = await RequestServerDeletionAsync(apiClient, ct, credential.AuthorizationCode)
                        .ConfigureAwait(false);
                }

                var failureMessage = GetDeletionFailureMessage(deletion);
                if (failureMessage is not null)
                    return Failed(failureMessage);

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

    private static string? GetDeletionFailureMessage(BackendApiResponse<AccountDeletionResult> response)
    {
        if (response.Status == BackendApiStatus.OfflineRejected)
            return AccountDeletionOfflineMessage;

        if (response.Status == BackendApiStatus.Unauthorized)
            return AccountDeletionSignInMessage;

        if (response.Status != BackendApiStatus.Success || response.Data is null)
            return AccountDeletionFailedMessage;

        if (response.Data.Succeeded)
            return null;

        if (response.Data.FailureCode == AccountDeletionFailureCode.AppleAuthorizationRequired)
            return AccountDeletionAppleAuthorizationMessage;

        return response.Data.FailureCode == AccountDeletionFailureCode.AppleRevocationFailed
            ? AccountDeletionAppleRevocationMessage
            : AccountDeletionFailedMessage;
    }

    private static Task<BackendApiResponse<AccountDeletionResult>> RequestServerDeletionAsync(
        IMuscleCutiesBackendApiClient apiClient,
        CancellationToken cancellationToken,
        string? authorizationCode = null) =>
        apiClient.SendMutationAsync<AccountDeletionRequest, AccountDeletionResult>(
            HttpMethod.Delete,
            DeleteAccountRoute,
            new AccountDeletionRequest(authorizationCode),
            cancellationToken: cancellationToken);

    private static AccountPurgeResult Failed(string message) =>
        new(false, false, false, [message]);
}
