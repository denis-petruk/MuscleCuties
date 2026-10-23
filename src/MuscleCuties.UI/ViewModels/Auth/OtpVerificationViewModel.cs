using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

public partial class OtpVerificationViewModel : ObservableObject, IDisposable
{
    private const string SendingCodeStatus = "Sending code...";
    private readonly ResendCountdownCoordinator _countdownCoordinator;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task> _navigateToProfileSetupAsync;
    private readonly IRegistrationOtpUseCase _otp;
    private readonly RegistrationOtpSession _session;
    private readonly TimeProvider _timeProvider;
    [ObservableProperty] private bool _canResend;
    private bool _completed;
    [ObservableProperty] private int _countdownSeconds = 60;
    private bool _disposed;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _infoMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isVerifying;

    [ObservableProperty] private string _otpCode = string.Empty;
    private CancellationTokenSource? _requestCts;
    [ObservableProperty] private string _resendButtonText = "Resend in 60s";

    public OtpVerificationViewModel(
        IRegistrationOtpUseCase otp,
        Func<Task> navigateToProfileSetupAsync,
        Func<Task> navigateBackAsync,
        TimeProvider? timeProvider = null)
    {
        _otp = otp ?? throw new ArgumentNullException(nameof(otp));
        _session = otp.OpenSession();
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateBackAsync = navigateBackAsync;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _countdownCoordinator = new ResendCountdownCoordinator((sec, canResend, text) =>
        {
            CountdownSeconds = sec;
            CanResend = canResend;
            ResendButtonText = text;
        }, _timeProvider);

        VerifyCommand = new AsyncRelayCommand(VerifyAsync);
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => CanResend && !IsBusy);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
        StartCountdown(60);
    }

    public string Email => _session.Email;
    public AsyncRelayCommand VerifyCommand { get; }
    public AsyncRelayCommand ResendCodeCommand { get; }
    public AsyncRelayCommand GoBackCommand { get; }

    public void Dispose()
    {
        _disposed = true;
        _countdownCoordinator.Dispose();
        _requestCts?.Cancel();
        _otp.HandOffSession(_session.SessionId);
        GC.SuppressFinalize(this);
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

    private async Task VerifyAsync()
    {
        if (IsBusy || _disposed || _completed)
            return;
        if (!_session.IsActive)
        {
            ErrorMessage = "Registration session expired. Please start over.";
            return;
        }

        var cleanCode = OtpCode.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
        {
            ErrorMessage = "Please enter a valid 6-digit verification code.";
            return;
        }

        IsBusy = true;
        IsVerifying = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        using var cts = new CancellationTokenSource();
        _requestCts = cts;
        try
        {
            var result = await _otp.VerifyAsync(
                new VerifyRegistrationOtpRequest(_session.SessionId, cleanCode), cts.Token);
            if (_disposed)
                return;
            if (result.Expired)
            {
                ErrorMessage = "Registration session expired. Please start over.";
                return;
            }

            if (!result.Verified)
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid code. Please check and try again.";
                return;
            }

            if (!_otp.ConfirmSession(_session.SessionId))
            {
                ErrorMessage = "Registration session expired. Please start over.";
                return;
            }

            _completed = true;
            StopCountdown();
            _otp.HandOffSession(_session.SessionId);
            await _navigateToProfileSetupAsync();
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OtpVerificationViewModel] Verification failed ({ex.GetType().Name}).");
            if (!_disposed)
                ErrorMessage = "Could not verify code. Please check your network and try again.";
        }
        finally
        {
            if (ReferenceEquals(_requestCts, cts))
                _requestCts = null;
            IsVerifying = false;
            IsBusy = false;
        }
    }

    private async Task ResendCodeAsync()
    {
        if (IsBusy || !CanResend || _disposed || _completed)
            return;
        if (!_session.IsActive)
        {
            ErrorMessage = "Registration session expired. Please start over.";
            return;
        }

        IsBusy = true;
        CanResend = false;
        ResendButtonText = SendingCodeStatus;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        using var cts = new CancellationTokenSource();
        _requestCts = cts;
        try
        {
            var result = await _otp.ResendAsync(new ResendRegistrationOtpRequest(_session.SessionId), cts.Token);
            if (_disposed)
                return;
            if (result.Expired)
            {
                ErrorMessage = "Registration session expired. Please start over.";
                return;
            }

            if (!result.Resent)
            {
                ErrorMessage = result.ErrorMessage ?? "Could not resend code. Please try again.";
                return;
            }

            OtpCode = string.Empty;
            InfoMessage = "A new verification code has been sent.";
            StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OtpVerificationViewModel] Resend failed ({ex.GetType().Name}).");
            if (!_disposed)
                ErrorMessage = "Could not resend code. Please check your network and try again.";
        }
        finally
        {
            if (ReferenceEquals(_requestCts, cts))
                _requestCts = null;
            if (!_disposed && ResendButtonText == SendingCodeStatus)
            {
                ResendButtonText = "Resend code";
                CanResend = true;
            }
            IsBusy = false;
        }
    }

    private async Task GoBackAsync()
    {
        _disposed = true;
        StopCountdown();
        _requestCts?.Cancel();
        if (!_completed)
            _otp.CancelSession(_session.SessionId);
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
