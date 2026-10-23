using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

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
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync);
        GoToPrivacyPolicyCommand = new AsyncRelayCommand(GoToPrivacyPolicyAsync);
    }

    public AsyncRelayCommand RegisterCommand { get; }
    public AsyncRelayCommand GoBackCommand { get; }
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    public AsyncRelayCommand GoToPrivacyPolicyCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;
    public bool IsPlatformSignInAvailable => _platformSignIn.IsAvailable;
    public bool IsAppleSignInAvailable => IsPlatformSignInAvailable;
    public string PlatformRegisterButtonText => _platformSignIn.RegisterButtonText;

    partial void OnHasAcceptedPrivacyNoticeChanged(bool value)
    {
        _privacyConsentAcceptedAtUtc = value ? DateTimeOffset.UtcNow : null;
    }

    private async Task GoToPrivacyPolicyAsync()
    {
        if (_navigateToPrivacyPolicyAsync is not null)
        {
            await _navigateToPrivacyPolicyAsync();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        CancelPendingRequest();
        GC.SuppressFinalize(this);
    }

    private async Task RegisterAsync()
    {
        if (IsBusy || _disposed)
            return;

        if (!HasAcceptedPrivacyNotice)
        {
            ErrorMessage = "Accept the privacy notice before creating an account.";
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
                ErrorMessage = result.ErrorMessage ?? "Email verification is unavailable. Please try again later.";
                return;
            }

            _ownedAttemptId = attemptId;
            if (_navigateToOtpVerificationAsync is null)
            {
                ErrorMessage = "Email verification is unavailable. Please try again later.";
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
                ErrorMessage = "We couldn't finish creating your account securely. Please try again.";
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

    public void CancelPendingRequest()
    {
        ++_requestVersion;
        _requestCts?.Cancel();
        _requestCts = null;
        if (_ownedAttemptId is { } attemptId)
            _registration.CancelAttempt(attemptId);
        _ownedAttemptId = null;
    }

    private async Task GoBackAsync()
    {
        Dispose();
        await _navigateBackAsync();
    }

    private async Task RedirectExistingAccountAsync(string email)
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        if (_navigateToExistingLoginAsync is null)
        {
            ErrorMessage = "You already have an account. Log in with your password.";
            return;
        }

        await _navigateToExistingLoginAsync(email.Trim().ToLowerInvariant());
    }

    private async Task SignInWithPlatformAsync()
    {
        if (IsBusy || _disposed)
            return;

        if (!HasAcceptedPrivacyNotice)
        {
            ErrorMessage = "Accept the privacy notice before creating an account.";
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
