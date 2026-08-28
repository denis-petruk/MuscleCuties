using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileHealthSyncViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IHealthSyncService _healthSyncService;
    private readonly Action _navigateBack;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _lastSyncedText = "No sync yet";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand ConnectAppleHealthCommand { get; }
    public AsyncRelayCommand ConnectWhoopCommand { get; }
    public RelayCommand BackCommand { get; }

    public ProfileHealthSyncViewModel(
        IAuthService authService,
        IHealthSyncService healthSyncService,
        Action navigateBack)
    {
        _authService = authService;
        _healthSyncService = healthSyncService;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConnectAppleHealthCommand = new AsyncRelayCommand(() => ConnectAsync(HealthDataSource.AppleHealth));
        ConnectWhoopCommand = new AsyncRelayCommand(() => ConnectAsync(HealthDataSource.Whoop));
        BackCommand = new RelayCommand(_navigateBack);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            await ApplyStatusAsync(userId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectAsync(HealthDataSource source)
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var result = await _healthSyncService.SyncAsync(userId, source);
            StatusMessage = result.Message;
            await ApplyStatusAsync(userId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyStatusAsync(int userId)
    {
        var status = await _healthSyncService.GetStatusAsync(userId);
        IsConnected = status.IsConnected;
        StatusText = status.SummaryText;
        LastSyncedText = status.LastSyncedAt is null
            ? "No sync yet"
            : $"Last sync {status.LastSyncedAt.Value.ToLocalTime():MMM d, h:mm tt}";
    }
}
