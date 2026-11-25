using Microsoft.Maui.Accessibility;
using MuscleCuties.Contracts.Account;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

/// <summary>
///     Account creation state: field validation, the privacy consent, and the handoff to the code step.
/// </summary>
public partial class RegisterViewModel : ObservableObject, IDisposable
{
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task>? _navigateToDashboardAsync;
    private readonly Func<string, Task>? _navigateToExistingLoginAsync;
    private readonly Func<Task>? _navigateToOtpVerificationAsync;
    private readonly Func<Task> _navigateToProfileSetupAsync;
    private readonly Func<Task>? _navigateToPrivacyPolicyAsync;
    private readonly IPlatformAccountSignInAdapter _platformSignIn;
    private readonly IAccountRegistrationUseCase _registration;
    private readonly IAccountSignInUseCase _signIn;
    private DateTimeOffset? _privacyConsentAcceptedAtUtc;
    private readonly IPhotoCacheAccountLifecycle? _photoCacheAccountLifecycle;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    private bool _disposed;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasAcceptedPrivacyNotice;
    private Guid? _ownedAttemptId;
    [ObservableProperty] private string _password = string.Empty;
    private CancellationTokenSource? _requestCts;
    private int _requestVersion;
    [ObservableProperty] private string _username = string.Empty;

    /// <summary>Takes the registration use cases and the navigation callbacks.</summary>
    public RegisterViewModel(
        IAccountRegistrationUseCase registration,
        IAccountSignInUseCase signIn,
        IPlatformAccountSignInAdapter platformSignIn,
        Func<Task> navigateToProfileSetupAsync,
        Func<Task> navigateBackAsync,
        Func<Task>? navigateToDashboardAsync = null,
        Func<string, Task>? navigateToExistingLoginAsync = null,
        Func<Task>? navigateToOtpVerificationAsync = null,
        Func<Task>? navigateToPrivacyPolicyAsync = null,
        IPhotoCacheAccountLifecycle? photoCacheAccountLifecycle = null)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _photoCacheAccountLifecycle = photoCacheAccountLifecycle;
        _platformSignIn = platformSignIn ?? throw new ArgumentNullException(nameof(platformSignIn));
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateBackAsync = navigateBackAsync;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToExistingLoginAsync = navigateToExistingLoginAsync;
        _navigateToOtpVerificationAsync = navigateToOtpVerificationAsync;
        _navigateToPrivacyPolicyAsync = navigateToPrivacyPolicyAsync;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsBusy);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync, () => !IsBusy);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync, () => !IsBusy);
        GoToPrivacyPolicyCommand = new AsyncRelayCommand(GoToPrivacyPolicyAsync, () => !IsBusy);
    }

    /// <summary>Registers the account.</summary>
    public AsyncRelayCommand RegisterCommand { get; }
    /// <summary>Returns to the previous page.</summary>
    public AsyncRelayCommand GoBackCommand { get; }
    /// <summary>Registers through the platform account.</summary>
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    /// <summary>Opens the privacy policy.</summary>
    public AsyncRelayCommand GoToPrivacyPolicyCommand { get; }
    /// <summary>Registers with Apple.</summary>
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;
    /// <summary>True when the platform can sign in.</summary>
    public bool IsPlatformSignInAvailable => _platformSignIn.IsAvailable;
    /// <summary>True when Apple sign-in is available on this device.</summary>
    public bool IsAppleSignInAvailable => IsPlatformSignInAvailable;
    /// <summary>The label for the platform registration button.</summary>
    public string PlatformRegisterButtonText => _platformSignIn.RegisterButtonText;

    /// <summary>Clears the error when the email changes.</summary>
    partial void OnEmailChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Clears the error when the username changes.</summary>
    partial void OnUsernameChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Clears the error when the password changes.</summary>
    partial void OnPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Checks the confirmation against the password.</summary>
    partial void OnConfirmPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Refreshes registration when the consent changes.</summary>
    partial void OnHasAcceptedPrivacyNoticeChanged(bool value)
    {
        _privacyConsentAcceptedAtUtc = value ? DateTimeOffset.UtcNow : null;
        ErrorMessage = string.Empty;
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        RegisterCommand.NotifyCanExecuteChanged();
        GoBackCommand.NotifyCanExecuteChanged();
        SignInWithPlatformCommand.NotifyCanExecuteChanged();
        GoToPrivacyPolicyCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Opens the privacy policy, which is what the consent the user is giving refers to.</summary>
    private async Task GoToPrivacyPolicyAsync()
    {
        if (_navigateToPrivacyPolicyAsync is not null)
        {
            await _navigateToPrivacyPolicyAsync();
        }
    }

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _disposed = true;
        CancelPendingRequest();
        GC.SuppressFinalize(this);
    }

    /// <summary>Creates the account once — a second tap or an answer after the page closed is ignored — and carries the</summary>
    /// registration attempt id so an abandoned attempt can be cancelled.</summary>
    private async Task RegisterAsync()
    {
        if (IsBusy || _disposed)
            return;

        if (!HasAcceptedPrivacyNotice)
        {
            ErrorMessage = "Ui_Auth_Error_PrivacyRequired";
            return;
        }

        var requestVersion = ++_requestVersion;
        using var cts = new CancellationTokenSource();
        _requestCts = cts;

        bool IsCurrent()
        {
            return !_disposed && requestVersion == _requestVersion && !cts.IsCancellationRequested;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _registration.StartAsync(new StartAccountRegistrationRequest(
                Email, Username, Password, ConfirmPassword,
                new RegistrationPrivacyConsent(PrivacyNotice.CurrentVersion, true,
                    _privacyConsentAcceptedAtUtc ?? DateTimeOffset.UtcNow)), cts.Token);

            if (!IsCurrent())
            {
                if (result.AttemptId is { } staleAttempt)
                    _registration.CancelAttempt(staleAttempt);
                return;
            }

            if (result.Status == AccountRegistrationStatus.ExistingEmail)
            {
                await RedirectExistingAccountAsync(result.ExistingEmail ?? Email);
                return;
            }

            if (result.Status == AccountRegistrationStatus.ReplacedOrCancelled)
                return;

            if (result.Status != AccountRegistrationStatus.Started || result.AttemptId is not { } attemptId)
            {
                ErrorMessage = result.ErrorMessage ?? "Ui_Auth_Error_VerificationUnavailable";
                return;
            }

            _ownedAttemptId = attemptId;
            if (_navigateToOtpVerificationAsync is null)
            {
                ErrorMessage = "Ui_Auth_Error_VerificationUnavailable";
                return;
            }

            // Ownership passes to the OTP page before navigation hides this page.
            _registration.HandOffAttempt(attemptId);
            _ownedAttemptId = null;
            await _navigateToOtpVerificationAsync();
        }
        catch (OperationCanceledException) when (!IsCurrent())
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RegisterViewModel] Registration failed ({ex.GetType().Name}).");
            if (IsCurrent())
                ErrorMessage = "Ui_Auth_Error_CreateAccount";
        }
        finally
        {
            if (_ownedAttemptId is { } attemptId)
                _registration.CancelAttempt(attemptId);
            _ownedAttemptId = null;
            if (ReferenceEquals(_requestCts, cts))
                _requestCts = null;
            IsBusy = false;
        }
    }

    /// <summary>Abandons the request in flight: bumps the version so its answer is discarded and cancels the attempt this</summary>
    /// page owns, so leaving the form does not leave a half-made account behind.</summary>
    public void CancelPendingRequest()
    {
        ++_requestVersion;
        _requestCts?.Cancel();
        _requestCts = null;
        if (_ownedAttemptId is { } attemptId)
            _registration.CancelAttempt(attemptId);
        _ownedAttemptId = null;
    }

    /// <summary>Closes the page, releasing the pending request first so a late answer cannot act on a form the user has</summary>
    /// left.</summary>
    private async Task GoBackAsync()
    {
        Dispose();
        await _navigateBackAsync();
    }

    /// <summary>Hands an already-registered address to the login page with the typed email, clearing the passwords it can no</summary>
    /// longer use.</summary>
    private async Task RedirectExistingAccountAsync(string email)
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        if (_navigateToExistingLoginAsync is null)
        {
            ErrorMessage = "Ui_Auth_Error_AccountExists";
            return;
        }

        await _navigateToExistingLoginAsync(email.Trim().ToLowerInvariant());
    }

    /// <summary>The platform sign-in path from the register page, which may land on an existing account or a new one.</summary>
    private async Task SignInWithPlatformAsync()
    {
        if (IsBusy || _disposed)
            return;

        if (!HasAcceptedPrivacyNotice)
        {
            ErrorMessage = "Ui_Auth_Error_PrivacyRequired";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await PlatformSignInCoordinator.ExecuteAsync(
                _platformSignIn,
                _signIn,
                ActivateAndNavigateAsync,
                msg => ErrorMessage = msg,
                nameof(RegisterViewModel));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Activates the new account's photo cache before navigating, so nothing from a previous account survives the</summary>
    /// switch.</summary>
    private async Task ActivateAndNavigateAsync(bool isComplete)
    {
        if (_photoCacheAccountLifecycle is not null)
            await _photoCacheAccountLifecycle.ActivateCurrentAccountAsync();

        if (isComplete && _navigateToDashboardAsync is not null)
            await _navigateToDashboardAsync();
        else
            await _navigateToProfileSetupAsync();
    }
}
