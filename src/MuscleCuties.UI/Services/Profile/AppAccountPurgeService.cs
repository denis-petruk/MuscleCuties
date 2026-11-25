using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Deletes the account on the server and then everything it left on the device: local state, cached</summary>
/// photos and keys.</summary>
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

    /// <summary>Takes the services the purge needs.</summary>
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

    /// <summary>Purges the account, reporting per step whether it succeeded. Local data is only cleared for the</summary>
    /// account the server confirmed deleting.</summary>
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

                    PlatformSignInResult? credential;
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

    /// <summary>Clears everything this device holds for the signed-in account and signs out, used when the account is</summary>
    /// already gone on the server.</summary>
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

    /// <summary>The message for a failed deletion: offline and expired sessions get their own wording, because both</summary>
    /// leave the account intact.</summary>
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

    /// <summary>Asks the server to delete the account.</summary>
    private static Task<BackendApiResponse<AccountDeletionResult>> RequestServerDeletionAsync(
        IMuscleCutiesBackendApiClient apiClient,
        CancellationToken cancellationToken,
        string? authorizationCode = null) =>
        apiClient.SendMutationAsync<AccountDeletionRequest, AccountDeletionResult>(
            HttpMethod.Delete,
            DeleteAccountRoute,
            new AccountDeletionRequest(authorizationCode),
            cancellationToken: cancellationToken);

    /// <summary>The result that reports a failed purge.</summary>
    private static AccountPurgeResult Failed(string message) =>
        new(false, false, false, [message]);
}
