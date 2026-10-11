using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileHealthSyncViewModel : ProfileSubViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IReadinessSyncOrchestrator _syncOrchestrator;
    private readonly IAppPreloadService _preloadService;

    [ObservableProperty] private string _lastSyncedText = "No sync yet";

    [ObservableProperty]
    private string _readinessExplanationText = "Numerical guidance is unavailable pending qualified review.";

    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _connectionBadgeText = "Tap to Connect";

    public ProfileHealthSyncViewModel(
        IAuthService authService,
        IReadinessSyncOrchestrator syncOrchestrator,
        IAppPreloadService preloadService,
        Func<Task> navigateBackAsync)
        : base(navigateBackAsync)
    {
        _authService = authService;
        _syncOrchestrator = syncOrchestrator;
        _preloadService = preloadService;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
    }

    [Obsolete("Use primary constructor with IReadinessSyncOrchestrator")]
    public ProfileHealthSyncViewModel(
        IAuthService authService,
        IProfileAccountService profileAccountService,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutService workoutService,
        IDashboardPlanner dashboardPlanner,
        IHealthSyncService healthSyncService,
        IAppPreloadService preloadService,
        Func<Task> navigateBackAsync)
        : this(
            authService,
            new ReadinessSyncOrchestrator(healthSyncService, profileAccountService, cycleService, nutritionService, workoutService, dashboardPlanner),
            preloadService,
            navigateBackAsync)
    {
    }

    public string ReadinessScoreText => ReadinessScore > 0
        ? ReadinessScore.ToString(CultureInfo.InvariantCulture)
        : "Unavailable";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public bool IsAppleHealthAvailable => _syncOrchestrator.IsAppleHealthAvailable;

    private async Task ConnectAsync()
    {
        if (!IsAppleHealthAvailable || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var result = await _syncOrchestrator.SyncAsync(userId, HealthDataSource.AppleHealth);
            if (result.IsConnected)
                _preloadService.InvalidateDashboard();
            StatusMessage = result.IsConnected ? "Apple Health updated." : result.Message;
            await ApplyStatusAsync(userId);
        }
        finally
        {
            IsBusy = false;
        }
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

    private async Task ApplyStatusAsync(int userId)
    {
        var summary = await _syncOrchestrator.GetProfileHealthSyncSummaryAsync(userId);
        StatusText = summary.StatusText;
        ConnectionBadgeText = summary.ConnectionBadgeText;
        LastSyncedText = summary.LastSyncedText;
        ReadinessScore = summary.ReadinessScore;
        ReadinessExplanationText = summary.ReadinessExplanationText;
    }

    partial void OnReadinessScoreChanged(int value)
    {
        OnPropertyChanged(nameof(ReadinessScoreText));
    }
}
