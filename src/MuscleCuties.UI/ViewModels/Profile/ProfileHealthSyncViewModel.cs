using MuscleCuties.Core.Services.Health;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isConnected;

    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private string _statusText = string.Empty;
    /// <summary>True when an error is shown.</summary>
    [ObservableProperty] private string _errorText = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>Takes the sync service and the account it belongs to; either may be absent, in which case the section</summary>
    /// reports itself unavailable instead of failing.</summary>
    public ProfileHealthSyncViewModel(IHealthSyncService? healthSync, ICurrentProfileUserIdProvider? userIdProvider)
    {
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => !IsBusy && IsConnected);
    }

    /// <summary>Refreshes the error flag when the text changes.</summary>
    partial void OnErrorTextChanged(string value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Connects the health data source.</summary>
    public IAsyncRelayCommand ConnectCommand { get; }
    /// <summary>Disconnects the health data source.</summary>
    public IAsyncRelayCommand DisconnectCommand { get; }

    /// <summary>Reads the current connection state and the last sync status, so the section shows what the device</summary>
    /// actually shares rather than what was last chosen here.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        ErrorText = string.Empty;
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
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks the platform for read access and records the account it is enabled for, surfacing a refusal as the</summary>
    /// section's error text.</summary>
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
        }
    }

    /// <summary>Stops syncing for this account without revoking the device permission, which only the system settings can</summary>
    /// do.</summary>
    private async Task DisconnectAsync()
    {
        if (_healthSync is null || _userIdProvider is null || IsBusy) return;

        if (Shell.Current is { } shell)
        {
            var confirmed = await shell.DisplayAlertAsync(
                UiTextCatalog.Get("Profile_HealthSync_DisconnectConfirmTitle"),
                UiTextCatalog.Get("Profile_HealthSync_DisconnectConfirmMessage"),
                UiTextCatalog.Get("Profile_HealthSync_Disconnect"),
                UiTextCatalog.Get("Common_Cancel"));
            if (!confirmed)
                return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
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
        }
    }

    /// <summary>True when a cached health summary exists.</summary>
    private async Task<bool> HasCachedSummaryAsync(int userId) =>
        await _healthSync!.GetCachedWeeklySummaryAsync(userId) is not null;
}
