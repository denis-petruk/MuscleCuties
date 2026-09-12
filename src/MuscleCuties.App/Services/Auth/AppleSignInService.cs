using MuscleCuties.Core.Services.Auth;
#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.App.Services.Auth;

public sealed class AppleSignInService : IAppleSignInService, IPlatformSignInService
{
    public Task<AppleSignInResult?> SignInAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        if (_activeController is not null)
            return Task.FromResult<AppleSignInResult?>(null);

        var completion = new TaskCompletionSource<AppleSignInResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() =>
            {
                ClearActiveSession();
                completion.TrySetCanceled(cancellationToken);
            });

        MainThread.BeginInvokeOnMainThread(() => StartAppleSignIn(completion));
        return completion.Task;
#else
        throw new PlatformNotSupportedException("Apple sign in is available on Apple devices.");
#endif
    }

    public string ProviderName => "Apple";
    public string LoginButtonText => "Log In with Apple";
    public string RegisterButtonText => "Sign in with Apple";

    public async Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default)
    {
        var appleAccount = await SignInAsync(cancellationToken);
        return appleAccount is null
            ? null
            : new PlatformSignInResult(
                "apple",
                appleAccount.UserIdentifier,
                appleAccount.Email,
                appleAccount.FullName);
    }
#if IOS || MACCATALYST
    private ASAuthorizationController? _activeController;
    private AppleAuthorizationDelegate? _activeDelegate;
    private ApplePresentationContextProvider? _activeContextProvider;
#endif

#if IOS || MACCATALYST
    private void StartAppleSignIn(TaskCompletionSource<AppleSignInResult?> completion)
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

            var controller = new ASAuthorizationController([request]);
            var authorizationDelegate = new AppleAuthorizationDelegate(completion, ClearActiveSession);
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
        Action clearActiveSession) : ASAuthorizationControllerDelegate
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

                completion.TrySetResult(new AppleSignInResult(
                    credential.User,
                    credential.Email,
                    BuildFullName(credential.FullName)));
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
