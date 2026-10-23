using MuscleCuties.Core.Services.Auth;
#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.UI.Services.Auth;

public sealed class AppleSignInService(
    MuscleCuties.Infrastructure.ApiClient.IMuscleCutiesBackendApiClient? apiClient = null) : IAppleSignInService, IPlatformSignInService
{
    private sealed record AppleNonceDto(string ChallengeId, string Nonce);

    public string ProviderName => "Apple";
    public string LoginButtonText => "Log In with Apple";
    public string RegisterButtonText => "Sign in with Apple";

    public async Task<AppleSignInResult?> SignInAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        if (_activeController is not null)
            return null;

        string? rawNonce = null;
        string? nonceChallengeId = null;

        if (apiClient is null)
            throw new InvalidOperationException("Apple sign in requires the account service.");

        var nonceResponse = await apiClient.SendMutationAsync<object, AppleNonceDto>(
            HttpMethod.Post,
            "/api/v1/auth/apple/nonce",
            new { },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (nonceResponse.Status != MuscleCuties.Infrastructure.ApiClient.BackendApiStatus.Success ||
            nonceResponse.Data is null || !Guid.TryParse(nonceResponse.Data.ChallengeId, out _) ||
            string.IsNullOrWhiteSpace(nonceResponse.Data.Nonce))
            throw new InvalidOperationException("Apple sign in could not start. Please try again.");

        rawNonce = nonceResponse.Data.Nonce;
        nonceChallengeId = nonceResponse.Data.ChallengeId;

        var completion = new TaskCompletionSource<AppleSignInResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() =>
            {
                ClearActiveSession();
                completion.TrySetCanceled(cancellationToken);
            });

        MainThread.BeginInvokeOnMainThread(() => StartAppleSignIn(completion, rawNonce, nonceChallengeId));
        return await completion.Task.ConfigureAwait(false);
#else
        throw new PlatformNotSupportedException("Apple sign in is available on Apple devices.");
#endif
    }

    public Task<AppleCredentialState> GetCredentialStateAsync(string appleUserId)
    {
#if IOS || MACCATALYST
        if (string.IsNullOrWhiteSpace(appleUserId))
            return Task.FromResult(AppleCredentialState.NotFound);

        var provider = new ASAuthorizationAppleIdProvider();
        var tcs = new TaskCompletionSource<AppleCredentialState>();
        provider.GetCredentialState(appleUserId, (state, error) =>
        {
            if (error is not null)
            {
                tcs.TrySetResult(AppleCredentialState.Error);
                return;
            }

            var mapped = state switch
            {
                ASAuthorizationAppleIdProviderCredentialState.Authorized => AppleCredentialState.Authorized,
                ASAuthorizationAppleIdProviderCredentialState.Revoked => AppleCredentialState.Revoked,
                ASAuthorizationAppleIdProviderCredentialState.NotFound => AppleCredentialState.NotFound,
                ASAuthorizationAppleIdProviderCredentialState.Transferred => AppleCredentialState.Transferred,
                _ => AppleCredentialState.Error
            };
            tcs.TrySetResult(mapped);
        });

        return tcs.Task;
#else
        return Task.FromResult(AppleCredentialState.Unsupported);
#endif
    }

    public async Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default)
    {
        var appleAccount = await SignInAsync(cancellationToken).ConfigureAwait(false);
        return appleAccount is null
            ? null
            : new PlatformSignInResult(
                "apple",
                appleAccount.UserIdentifier,
                appleAccount.Email,
                appleAccount.FullName,
                appleAccount.IdentityToken,
                appleAccount.Nonce,
                appleAccount.NonceChallengeId,
                appleAccount.AuthorizationCode);
    }
#if IOS || MACCATALYST
    private ASAuthorizationController? _activeController;
    private AppleAuthorizationDelegate? _activeDelegate;
    private ApplePresentationContextProvider? _activeContextProvider;
#endif

#if IOS || MACCATALYST
    private void StartAppleSignIn(
        TaskCompletionSource<AppleSignInResult?> completion,
        string? rawNonce,
        string? nonceChallengeId)
    {
        try
        {
            var provider = new ASAuthorizationAppleIdProvider();
            var request = provider.CreateRequest();
            request.RequestedScopes =
            [
                ASAuthorizationScope.FullName,
                ASAuthorizationScope.Email
            ];

            if (!string.IsNullOrWhiteSpace(rawNonce))
            {
                var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawNonce));
                request.Nonce = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            var controller = new ASAuthorizationController([request]);
            var authorizationDelegate = new AppleAuthorizationDelegate(completion, ClearActiveSession, rawNonce, nonceChallengeId);
            var contextProvider = new ApplePresentationContextProvider();

            _activeController = controller;
            _activeDelegate = authorizationDelegate;
            _activeContextProvider = contextProvider;

            controller.Delegate = authorizationDelegate;
            controller.PresentationContextProvider = contextProvider;
            controller.PerformRequests();
        }
        catch (Exception ex)
        {
            ClearActiveSession();
            completion.TrySetException(ex);
        }
    }

    private void ClearActiveSession()
    {
        _activeController = null;
        _activeDelegate = null;
        _activeContextProvider = null;
    }

    private sealed class AppleAuthorizationDelegate(
        TaskCompletionSource<AppleSignInResult?> completion,
        Action clearActiveSession,
        string? rawNonce,
        string? nonceChallengeId) : ASAuthorizationControllerDelegate
    {
        public override void DidComplete(
            ASAuthorizationController controller,
            ASAuthorization authorization)
        {
            try
            {
                var credential = authorization.GetCredential<ASAuthorizationAppleIdCredential>();
                if (credential is null ||
                    string.IsNullOrWhiteSpace(credential.User))
                {
                    completion.TrySetResult(null);
                    return;
                }

                string? identityToken = null;
                if (credential.IdentityToken != null)
                {
                    identityToken = System.Text.Encoding.UTF8.GetString(credential.IdentityToken.ToArray());
                }

                string? authorizationCode = null;
                if (credential.AuthorizationCode != null)
                {
                    authorizationCode = System.Text.Encoding.UTF8.GetString(credential.AuthorizationCode.ToArray());
                }

                completion.TrySetResult(new AppleSignInResult(
                    credential.User,
                    credential.Email,
                    BuildFullName(credential.FullName),
                    identityToken,
                    rawNonce,
                    nonceChallengeId,
                    authorizationCode));
            }
            finally
            {
                clearActiveSession();
            }
        }

        public override void DidComplete(
            ASAuthorizationController controller,
            NSError error)
        {
            try
            {
                if (error.Code == 1001)
                {
                    completion.TrySetResult(null);
                    return;
                }

                completion.TrySetException(new InvalidOperationException(BuildErrorMessage(error)));
            }
            finally
            {
                clearActiveSession();
            }
        }

        private static string BuildErrorMessage(NSError error)
        {
            if (error.Domain == "com.apple.AuthenticationServices.AuthorizationError" &&
                error.Code == 1000)
                return
                    "Apple login is not ready for this build. Enable Sign in with Apple in the provisioning profile, then try again.";

            return error.LocalizedDescription ?? "Apple login could not finish. Please try again.";
        }

        private static string? BuildFullName(NSPersonNameComponents? fullName)
        {
            if (fullName is null)
                return null;

            var parts = new[] { fullName.GivenName, fullName.FamilyName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim());

            var displayName = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }
    }

    private sealed class ApplePresentationContextProvider :
        NSObject,
        IASAuthorizationControllerPresentationContextProviding
    {
        public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
        {
            var currentWindow = Platform.GetCurrentUIViewController()?.View?.Window;
            if (currentWindow is not null)
                return currentWindow;

            var sceneWindow = UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(window => window.IsKeyWindow);

            return sceneWindow ?? throw new InvalidOperationException("No active window for Apple sign in.");
        }
    }
#endif
}
