#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
#endif
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App.Services.Auth;

public sealed class AppleSignInService : IAppleSignInService
{
#if IOS || MACCATALYST
    private ASAuthorizationController? _activeController;
    private AppleAuthorizationDelegate? _activeDelegate;
    private ApplePresentationContextProvider? _activeContextProvider;
#endif

    public Task<AppleSignInResult?> SignInAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        var completion = new TaskCompletionSource<AppleSignInResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                ClearActiveSession();
                completion.TrySetCanceled(cancellationToken);
            });
        }

        MainThread.BeginInvokeOnMainThread(() => StartAppleSignIn(completion));
        return completion.Task;
#else
        throw new PlatformNotSupportedException("Apple sign in is available on Apple devices.");
#endif
    }

#if IOS || MACCATALYST
    private void StartAppleSignIn(TaskCompletionSource<AppleSignInResult?> completion)
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
                completion.TrySetException(new InvalidOperationException(
                    error.LocalizedDescription ?? "Apple sign in could not finish."));
            }
            finally
            {
                clearActiveSession();
            }
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
