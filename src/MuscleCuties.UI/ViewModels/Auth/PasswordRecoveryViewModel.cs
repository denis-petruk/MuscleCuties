using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

/// <summary>The recovery wizard's steps, numbered so the progress line and the visible panel agree on where the user</summary>
/// is.</summary>
public enum PasswordRecoveryStep
{
    EnterIdentifier = 1,
    EnterCode = 2,
    SetNewPassword = 3,
    Completed = 4
}

/// <summary>
///     Password recovery state machine: request a code, verify it, then set a new password.
/// </summary>
public partial class PasswordRecoveryViewModel : ObservableObject, IDisposable
{
    private readonly ResendCountdownCoordinator _countdownCoordinator;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Action<string>? _onRecoverySuccessNavigateToLogin;
    private readonly IAccountPasswordValidator _passwordValidator;
    private readonly IAccountRecoveryUseCase _recoveryService;
    [ObservableProperty] private bool _canResend;
    [ObservableProperty] private string _challengeId = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private int _countdownSeconds = 60;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _emailOrUsername = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _infoMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _maskedEmail = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _otpCode = string.Empty;
    [ObservableProperty] private string _resendButtonText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Auth_PasswordRecovery_ResendCode");

    [ObservableProperty] private PasswordRecoveryStep _step = PasswordRecoveryStep.EnterIdentifier;
    [ObservableProperty] private string _verificationToken = string.Empty;

    /// <summary>Takes the recovery use case, the validator and the navigation callbacks.</summary>
    public PasswordRecoveryViewModel(
        IAccountRecoveryUseCase recoveryService,
        IAccountPasswordValidator passwordValidator,
        Func<Task> navigateBackAsync,
        Action<string>? onRecoverySuccessNavigateToLogin = null,
        TimeProvider? timeProvider = null)
    {
        _recoveryService = recoveryService;
        _passwordValidator = passwordValidator;
        _navigateBackAsync = navigateBackAsync;
        _onRecoverySuccessNavigateToLogin = onRecoverySuccessNavigateToLogin;
        _countdownCoordinator = new ResendCountdownCoordinator((seconds, canResend, text) =>
        {
            CountdownSeconds = seconds;
            CanResend = canResend;
            ResendButtonText = text;
        }, MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Auth_PasswordRecovery_ResendCode"),
            seconds => MuscleCuties.UI.Services.Localization.UiTextCatalog.Format("Auth_Otp_ResendInFormat", seconds),
            timeProvider);

        StartRecoveryCommand = new AsyncRelayCommand(StartRecoveryAsync, () => !IsBusy);
        VerifyCodeCommand = new AsyncRelayCommand(VerifyCodeAsync, () => !IsBusy);
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => CanResend && !IsBusy);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => !IsBusy);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync, () => !IsBusy);
    }

    /// <summary>Starts recovery for the entered identifier.</summary>
    public AsyncRelayCommand StartRecoveryCommand { get; }
    /// <summary>Verifies the recovery code.</summary>
    public AsyncRelayCommand VerifyCodeCommand { get; }
    /// <summary>Requests a new recovery code.</summary>
    public AsyncRelayCommand ResendCodeCommand { get; }
    /// <summary>Sets the new password.</summary>
    public AsyncRelayCommand ResetPasswordCommand { get; }
    /// <summary>Returns to the previous page.</summary>
    public AsyncRelayCommand GoBackCommand { get; }

    /// <summary>True on the identifier step.</summary>
    public bool IsStep1 => Step == PasswordRecoveryStep.EnterIdentifier;
    /// <summary>True on the code step.</summary>
    public bool IsStep2 => Step == PasswordRecoveryStep.EnterCode;
    /// <summary>True on the new password step.</summary>
    public bool IsStep3 => Step == PasswordRecoveryStep.SetNewPassword;
    /// <summary>True once the recovery is complete.</summary>
    public bool IsStep4 => Step == PasswordRecoveryStep.Completed;

    /// <summary>The progress line names the current step by the same numbering the panels use.</summary>
    public string StepProgressText => MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
        "Auth_PasswordRecovery_StepFormat",
        Step switch
        {
            PasswordRecoveryStep.EnterCode => 2,
            PasswordRecoveryStep.SetNewPassword or PasswordRecoveryStep.Completed => 3,
            _ => 1
        });

    /// <summary>Stops the resend countdown.</summary>
    public void Dispose()
    {
        _countdownCoordinator.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Refreshes the step flags when the step changes.</summary>
    partial void OnStepChanged(PasswordRecoveryStep value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(StepProgressText));
    }

    /// <summary>Clears the error when the identifier changes.</summary>
    partial void OnEmailOrUsernameChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Refreshes the state when the code changes.</summary>
    partial void OnOtpCodeChanged(string value)
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrEmpty(value))
            return;

        var digitsOnly = OtpCodeRules.DigitsOnly(value);
        if (!string.Equals(digitsOnly, value, StringComparison.Ordinal))
            OtpCode = digitsOnly;
    }

    /// <summary>Checks the new password as it is typed.</summary>
    partial void OnNewPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Checks the confirmation against the new password.</summary>
    partial void OnConfirmPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Refreshes the resend state when the countdown ends.</summary>
    partial void OnCanResendChanged(bool value)
    {
        ResendCodeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        StartRecoveryCommand.NotifyCanExecuteChanged();
        VerifyCodeCommand.NotifyCanExecuteChanged();
        ResendCodeCommand.NotifyCanExecuteChanged();
        ResetPasswordCommand.NotifyCanExecuteChanged();
        GoBackCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Asks for a recovery code for the identifier entered and moves to the code step only when the request was</summary>
    /// accepted.</summary>
    private async Task StartRecoveryAsync()
    {
        if (IsBusy) return;

        var input = EmailOrUsername.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            ErrorMessage = "Ui_Auth_Error_IdentifierRequired";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            var result = await _recoveryService.StartAsync(new StartAccountRecoveryRequest(input));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Ui_Auth_Error_StartRecovery";
                return;
            }

            ChallengeId = result.ChallengeId ?? string.Empty;
            Email = result.Email ?? string.Empty;
            MaskedEmail = result.MaskedEmail ?? Email;
            Step = PasswordRecoveryStep.EnterCode;
            _countdownCoordinator.StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Start recovery failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Common_Error_ConnectionRetry";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Checks the recovery code and advances to the password step on success, leaving the user on the code step</summary>
    /// with the reason when it is refused.</summary>
    private async Task VerifyCodeAsync()
    {
        if (IsBusy) return;

        var cleanCode = OtpCode.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
        {
            ErrorMessage = "Ui_Auth_Error_InvalidCode";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            var result = await _recoveryService.VerifyAsync(new VerifyAccountRecoveryRequest(
                ChallengeId,
                Email,
                cleanCode));

            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.VerificationToken))
            {
                ErrorMessage = result.ErrorMessage ?? "Ui_Auth_Error_InvalidOrExpiredCode";
                return;
            }

            VerificationToken = result.VerificationToken;
            _countdownCoordinator.StopCountdown();
            Step = PasswordRecoveryStep.SetNewPassword;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Verify code failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Auth_Error_VerifyCode";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Sends a fresh code once the countdown allows it, restarting the countdown afterwards.</summary>
    private async Task ResendCodeAsync()
    {
        if (IsBusy || !CanResend) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            var result = await _recoveryService.ResendAsync(new ResendAccountRecoveryRequest(ChallengeId, Email));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Ui_Auth_Error_ResendCode";
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ChallengeId)) ChallengeId = result.ChallengeId;

            InfoMessage = "Ui_Auth_Info_CodeSent";
            _countdownCoordinator.StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Resend code failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Auth_Error_ResendCode";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Sets the new password with the verified session and finishes the wizard; a refusal keeps the session so the</summary>
    /// user can try again without redoing the code.</summary>
    private async Task ResetPasswordAsync()
    {
        if (IsBusy) return;

        if (!_passwordValidator.TryValidateConfirmation(NewPassword, ConfirmPassword, out var passwordError))
        {
            ErrorMessage = passwordError;
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            var result = await _recoveryService.ResetAsync(new ResetAccountPasswordRequest(
                Email,
                VerificationToken,
                NewPassword));

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Ui_Auth_Error_PasswordUpdate";
                return;
            }

            Step = PasswordRecoveryStep.Completed;
            InfoMessage = "Ui_Auth_Info_PasswordUpdated";

            if (_onRecoverySuccessNavigateToLogin != null)
                _onRecoverySuccessNavigateToLogin(Email);
            else
                await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Reset password failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Auth_Error_PasswordUpdate";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Leaves recovery and clears the stored session for this account, so an abandoned reset cannot be resumed</summary>
    /// later by someone else.</summary>
    private async Task GoBackAsync()
    {
        _countdownCoordinator.StopCountdown();
        _recoveryService.ClearSession(Email);

        if (Step == PasswordRecoveryStep.EnterCode)
        {
            Step = PasswordRecoveryStep.EnterIdentifier;
            OtpCode = string.Empty;
            ErrorMessage = string.Empty;
            InfoMessage = string.Empty;
            return;
        }

        await _navigateBackAsync();
    }


}
