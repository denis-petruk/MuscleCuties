using MuscleCuties.Core.Services.Auth;
#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.UI.Services.Auth;

/// <summary>Sign in with Apple. The server issues a nonce first, the system sheet signs the hashed nonce,</summary>
/// and the result carries the identity token plus the raw nonce the server verifies it against.</summary>
public sealed class AppleSignInService(
    MuscleCuties.Infrastructure.ApiClient.IMuscleCutiesBackendApiClient? apiClient = null) : IAppleSignInService, IPlatformSignInService
{
    /// <summary>The challenge the server issued, and the nonce that must be hashed into the Apple request.</summary>
    private sealed record AppleNonceDto(string ChallengeId, string Nonce);

    /// <summary>Provider id sent with the credential and used in messages.</summary>
    public string ProviderName => "Apple";
    /// <summary>Label for the sign-in screen's Apple button.</summary>
    public string LoginButtonText => "Log In with Apple";
    /// <summary>Label for the registration screen's Apple button, worded for creating an account.</summary>
    public string RegisterButtonText => "Sign in with Apple";

    /// <summary>Requests a nonce, presents the Apple sheet, and returns the credential. Returns null when the</summary>
    /// person cancelled or a sheet is already open, so cancel is not reported as an error.</summary>
    public async Task<PlatformSignInResult?> SignInAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        if (_activeController is not null)
            return null;

        string? rawNonce;
        string? nonceChallengeId;

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

        var completion = new TaskCompletionSource<PlatformSignInResult?>(
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

    /// <summary>Asks Apple whether the stored Apple identity is still authorized, which is how a revoked</summary>
    /// sign-in is detected before the next request fails.</summary>
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

    /// <summary>Provider-neutral entry point used by the adapter.</summary>
    public Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default)
    {
        return SignInAsync(cancellationToken);
    }
#if IOS || MACCATALYST
    private ASAuthorizationController? _activeController;
    private AppleAuthorizationDelegate? _activeDelegate;
    private ApplePresentationContextProvider? _activeContextProvider;
#endif

#if IOS || MACCATALYST
    /// <summary>Starts Apple's sign-in sheet and completes the caller with the result.</summary>
    private void StartAppleSignIn(
        TaskCompletionSource<PlatformSignInResult?> completion,
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

    /// <summary>Drops the retained controller, delegate and context provider once the sheet has finished, so a</summary>
    /// second attempt is not blocked by a stale session.</summary>
    private void ClearActiveSession()
    {
        _activeController = null;
        _activeDelegate = null;
        _activeContextProvider = null;
    }

    /// <summary>Receives the Apple result and translates it: a cancelled or empty credential completes with null,</summary>
    /// any other failure completes with an error the screen can show.</summary>
    private sealed class AppleAuthorizationDelegate(
        TaskCompletionSource<PlatformSignInResult?> completion,
        Action clearActiveSession,
        string? rawNonce,
        string? nonceChallengeId) : ASAuthorizationControllerDelegate
    {
        /// <summary>Completes the sign-in with the credential Apple returned.</summary>
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

                completion.TrySetResult(new PlatformSignInResult(
                    "apple",
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

        /// <summary>Completes the sign-in with the error Apple returned.</summary>
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

        /// <summary>Turns the common provisioning-profile failure into an actionable message instead of the raw</summary>
        /// system text.</summary>
        private static string BuildErrorMessage(NSError error)
        {
            if (error.Domain == "com.apple.AuthenticationServices.AuthorizationError" &&
                error.Code == 1000)
                return
                    "Apple login is not ready for this build. Enable Sign in with Apple in the provisioning profile, then try again.";

            return error.LocalizedDescription ?? "Apple login could not finish. Please try again.";
        }

        /// <summary>Joins the given and family name Apple returned; Apple only supplies them on first sign-in.</summary>
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

    /// <summary>Supplies the window the Apple sheet is presented over, falling back to the key window of any</summary>
    /// connected scene.</summary>
    private sealed class ApplePresentationContextProvider :
        NSObject,
        IASAuthorizationControllerPresentationContextProviding
    {
        /// <summary>The window to present over; throws when there is none, because a sheet with no anchor cannot be</summary>
        /// shown at all.</summary>
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
