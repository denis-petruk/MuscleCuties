using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

public enum PasswordRecoveryStep
{
    EnterIdentifier = 1,
    EnterCode = 2,
    SetNewPassword = 3,
    Completed = 4
}

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
    [ObservableProperty] private string _resendButtonText = "Resend code";

    [ObservableProperty] private PasswordRecoveryStep _step = PasswordRecoveryStep.EnterIdentifier;
    [ObservableProperty] private string _verificationToken = string.Empty;

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
        }, timeProvider);

        StartRecoveryCommand = new AsyncRelayCommand(StartRecoveryAsync);
        VerifyCodeCommand = new AsyncRelayCommand(VerifyCodeAsync);
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => CanResend && !IsBusy);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
    }

    public AsyncRelayCommand StartRecoveryCommand { get; }
    public AsyncRelayCommand VerifyCodeCommand { get; }
    public AsyncRelayCommand ResendCodeCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }
    public AsyncRelayCommand GoBackCommand { get; }

    public bool IsStep1 => Step == PasswordRecoveryStep.EnterIdentifier;
    public bool IsStep2 => Step == PasswordRecoveryStep.EnterCode;
    public bool IsStep3 => Step == PasswordRecoveryStep.SetNewPassword;
    public bool IsStep4 => Step == PasswordRecoveryStep.Completed;

    public void Dispose()
    {
        _countdownCoordinator.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnStepChanged(PasswordRecoveryStep value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
    }

    partial void OnOtpCodeChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    partial void OnCanResendChanged(bool value)
    {
        ResendCodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ResendCodeCommand.NotifyCanExecuteChanged();
    }

    private async Task StartRecoveryAsync()
    {
        if (IsBusy) return;

        var input = EmailOrUsername.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            ErrorMessage = "Please enter your email or username.";
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
                ErrorMessage = result.ErrorMessage ?? "Could not begin recovery. Please try again.";
                return;
            }

            ChallengeId = result.ChallengeId ?? string.Empty;
            Email = result.Email ?? string.Empty;
            MaskedEmail = result.MaskedEmail ?? Email;
            Step = PasswordRecoveryStep.EnterCode;
            StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Start recovery failed ({ex.GetType().Name}).");
            ErrorMessage = "Something went wrong. Please check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task VerifyCodeAsync()
    {
        if (IsBusy) return;

        var cleanCode = OtpCode.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
        {
            ErrorMessage = "Please enter a valid 6-digit code.";
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
                ErrorMessage = result.ErrorMessage ?? "Invalid or expired code. Please try again.";
                return;
            }

            VerificationToken = result.VerificationToken;
            StopCountdown();
            Step = PasswordRecoveryStep.SetNewPassword;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Verify code failed ({ex.GetType().Name}).");
            ErrorMessage = "Could not verify code. Please check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
                ErrorMessage = result.ErrorMessage ?? "Could not resend code. Please try again.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ChallengeId)) ChallengeId = result.ChallengeId;

            InfoMessage = "A new verification code has been sent.";
            StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Resend code failed ({ex.GetType().Name}).");
            ErrorMessage = "Could not resend code. Please check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
                ErrorMessage = result.ErrorMessage ?? "Could not reset password. Please try again.";
                return;
            }

            Step = PasswordRecoveryStep.Completed;
            InfoMessage = "Your password has been successfully updated.";

            if (_onRecoverySuccessNavigateToLogin != null)
                _onRecoverySuccessNavigateToLogin(Email);
            else
                await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PasswordRecoveryViewModel] Reset password failed ({ex.GetType().Name}).");
            ErrorMessage = "Failed to update password securely. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GoBackAsync()
    {
        StopCountdown();
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

    private void StartCountdown(int seconds)
    {
        _countdownCoordinator.StartCountdown(seconds);
    }

    private void StopCountdown()
    {
        _countdownCoordinator.StopCountdown();
    }
}
