using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

public partial class OtpVerificationViewModel : ObservableObject, IDisposable
{
    private const string SendingCodeKey = "Auth_Otp_SendingCode";
    private const string ResendCodeKey = "Auth_Otp_ResendCode";

    private static string SendingCodeStatus => UiTextCatalog.Get(SendingCodeKey);
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
    [ObservableProperty] private bool _isSessionExpired;
    [ObservableProperty] private bool _isVerifying;

    [ObservableProperty] private string _otpCode = string.Empty;
    private CancellationTokenSource? _requestCts;
    [ObservableProperty] private string _resendButtonText = UiTextCatalog.Get(ResendCodeKey);

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
        }, UiTextCatalog.Get(ResendCodeKey), seconds => UiTextCatalog.Format("Auth_Otp_ResendInFormat", seconds),
            _timeProvider);

        VerifyCommand = new AsyncRelayCommand(VerifyAsync, () => CanVerify);
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => CanResendNow);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync, () => !IsBusy);
        _countdownCoordinator.StartCountdown(60);
    }

    public string Email => _session.Email;
    public bool CanVerify => !IsBusy && !IsSessionExpired;
    public bool CanResendNow => CanResend && !IsBusy && !IsSessionExpired;
    public bool ShowCountdownText => !CanResend && !IsSessionExpired;
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
        if (string.IsNullOrEmpty(value))
            return;

        var digitsOnly = OtpCodeRules.DigitsOnly(value);
        if (!string.Equals(digitsOnly, value, StringComparison.Ordinal))
            OtpCode = digitsOnly;
    }

    partial void OnCanResendChanged(bool value)
    {
        OnPropertyChanged(nameof(CanResendNow));
        OnPropertyChanged(nameof(ShowCountdownText));
        ResendCodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSessionExpiredChanged(bool value)
    {
        OnPropertyChanged(nameof(CanVerify));
        OnPropertyChanged(nameof(CanResendNow));
        OnPropertyChanged(nameof(ShowCountdownText));
        VerifyCommand.NotifyCanExecuteChanged();
        ResendCodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanVerify));
        OnPropertyChanged(nameof(CanResendNow));
        VerifyCommand.NotifyCanExecuteChanged();
        ResendCodeCommand.NotifyCanExecuteChanged();
        GoBackCommand.NotifyCanExecuteChanged();
    }

    private async Task VerifyAsync()
    {
        if (IsBusy || _disposed || _completed)
            return;
        if (!_session.IsActive)
        {
            IsSessionExpired = true;
            ErrorMessage = "Ui_Auth_Error_SessionExpired";
            return;
        }

        var cleanCode = OtpCode.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
        {
            ErrorMessage = "Ui_Auth_Error_InvalidVerificationCode";
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
                IsSessionExpired = true;
                _countdownCoordinator.StopCountdown();
                ErrorMessage = "Ui_Auth_Error_SessionExpired";
                return;
            }

            if (!result.Verified)
            {
                ErrorMessage = "Ui_Auth_Error_IncorrectVerificationCode";
                return;
            }

            if (!_otp.ConfirmSession(_session.SessionId))
            {
                IsSessionExpired = true;
                _countdownCoordinator.StopCountdown();
                ErrorMessage = "Ui_Auth_Error_SessionExpired";
                return;
            }

            _completed = true;
            _countdownCoordinator.StopCountdown();
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
                ErrorMessage = "Ui_Auth_Error_VerifyCode";
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
            ErrorMessage = "Ui_Auth_Error_SessionExpired";
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
                ErrorMessage = "Ui_Auth_Error_SessionExpired";
                return;
            }

            if (!result.Resent)
            {
                ErrorMessage = "Ui_Auth_Error_ResendCode";
                return;
            }

            OtpCode = string.Empty;
            InfoMessage = "Ui_Auth_Info_CodeSent";
            _countdownCoordinator.StartCountdown(result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OtpVerificationViewModel] Resend failed ({ex.GetType().Name}).");
            if (!_disposed)
                ErrorMessage = "Ui_Auth_Error_ResendCode";
        }
        finally
        {
            if (ReferenceEquals(_requestCts, cts))
                _requestCts = null;
            if (!_disposed && ResendButtonText == SendingCodeStatus)
            {
                ResendButtonText = UiTextCatalog.Get(ResendCodeKey);
                CanResend = true;
            }
            IsBusy = false;
        }
    }

    private async Task GoBackAsync()
    {
        _disposed = true;
        _countdownCoordinator.StopCountdown();
        _requestCts?.Cancel();
        if (!_completed)
            _otp.CancelSession(_session.SessionId);
        await _navigateBackAsync();
    }


}
