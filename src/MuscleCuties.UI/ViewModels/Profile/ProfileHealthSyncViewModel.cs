using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
/// Apple Health sync controls on the privacy page. Reads only; never requests HealthKit access on open.
/// Access is requested only from an explicit Connect tap (owner decision: contextual prompt).
/// </summary>
public partial class ProfileHealthSyncViewModel : ObservableObject
{
    private readonly IHealthSyncService? _healthSync;
    private readonly ICurrentProfileUserIdProvider? _userIdProvider;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public ProfileHealthSyncViewModel(IHealthSyncService? healthSync, ICurrentProfileUserIdProvider? userIdProvider)
    {
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => !IsBusy && IsConnected);
    }

    partial void OnErrorTextChanged(string value) => OnPropertyChanged(nameof(HasError));

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }

    public async Task RefreshAsync()
    {
        if (_healthSync is null || _userIdProvider is null)
        {
            IsAvailable = false;
            StatusText = UiTextCatalog.Get("Profile_HealthSync_Unavailable");
            return;
        }
        IsAvailable = _healthSync.IsAvailable;
        if (!IsAvailable)
        {
            StatusText = UiTextCatalog.Get("Profile_HealthSync_Unavailable");
            return;
        }
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            var status = await _healthSync.GetStatusAsync(userId);
            IsConnected = status.IsConnected;
            StatusText = status.IsConnected
                ? UiTextCatalog.Format("Profile_HealthSync_Connected", status.SummaryText)
                : UiTextCatalog.Get("Profile_HealthSync_NotConnected");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileHealthSyncViewModel] Status failed ({ex.GetType().Name}).");
            ErrorText = UiTextCatalog.Get("Profile_HealthSync_Error");
        }
    }

    private async Task ConnectAsync()
    {
        if (_healthSync is null || _userIdProvider is null || IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            // Explicit user action: this is the only place that triggers the system HealthKit sheet from Profile.
            var result = await _healthSync.SyncAsync(userId, _healthSync.DeviceHealthSource);
            IsConnected = result.Summary is not null || await HasCachedSummaryAsync(userId);
            StatusText = IsConnected
                ? UiTextCatalog.Get("Profile_HealthSync_Connected_Updated")
                : UiTextCatalog.Get("Profile_HealthSync_NoData");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileHealthSyncViewModel] Connect failed ({ex.GetType().Name}).");
            ErrorText = UiTextCatalog.Get("Profile_HealthSync_Error");
        }
        finally
        {
            IsBusy = false;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task DisconnectAsync()
    {
        if (_healthSync is null || _userIdProvider is null || IsBusy) return;
        IsBusy = true;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            await _healthSync.DisconnectAsync(userId);
            IsConnected = false;
            StatusText = UiTextCatalog.Get("Profile_HealthSync_NotConnected");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileHealthSyncViewModel] Disconnect failed ({ex.GetType().Name}).");
            ErrorText = UiTextCatalog.Get("Profile_HealthSync_Error");
        }
        finally
        {
            IsBusy = false;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task<bool> HasCachedSummaryAsync(int userId) =>
        await _healthSync!.GetCachedWeeklySummaryAsync(userId) is not null;
}
