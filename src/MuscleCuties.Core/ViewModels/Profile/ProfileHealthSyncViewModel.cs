using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileHealthSyncViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly IDashboardPlanner _dashboardPlanner;
    private readonly IHealthSyncService _healthSyncService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly INutritionService _nutritionService;
    private readonly IUserRepository _userRepository;
    private readonly IWorkoutService _workoutService;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _lastSyncedText = "No sync yet";

    [ObservableProperty]
    private string _readinessExplanationText = "Estimated from cycle, nutrition logs, and today's plan.";

    [ObservableProperty] private int _readinessScore = 72;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _statusText = "Not connected";

    public ProfileHealthSyncViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutService workoutService,
        IDashboardPlanner dashboardPlanner,
        IHealthSyncService healthSyncService,
        Func<Task> navigateBackAsync)
    {
        _authService = authService;
        _userRepository = userRepository;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        _workoutService = workoutService;
        _dashboardPlanner = dashboardPlanner;
        _healthSyncService = healthSyncService;
        _navigateBackAsync = navigateBackAsync;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    public string ReadinessScoreText => ReadinessScore > 0 ? ReadinessScore.ToString() : "--";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

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
        var status = await _healthSyncService.GetStatusAsync(userId);
        var healthSummary = await _healthSyncService.GetCachedWeeklySummaryAsync(userId);
        var profile = await _userRepository.GetProfileAsync(userId);
        var prediction = await _cycleService.GetPredictionAsync(userId);
        var phase = prediction.CurrentPhase;
        var targets = await _nutritionService.CalculateDailyTargetsAsync(userId, phase);
        var consumed = await _nutritionService.GetConsumedTotalsAsync(userId, DateTime.Today);
        var workoutSummary = await _workoutService.GetTodaysSummaryAsync(userId, phase, DateTime.Today);
        var supportSummary = _dashboardPlanner.BuildSupportSummary(
            prediction,
            phase,
            targets.Calories <= 0f ? 0f : Math.Clamp(consumed.Calories / targets.Calories, 0f, 1f),
            profile?.Weight,
            profile?.WorkoutDaysPerWeek ?? 0,
            workoutSummary,
            healthSummary);

        StatusText = status.SummaryText;
        LastSyncedText = status.LastSyncedAt is null
            ? "No sync yet"
            : $"Last sync {status.LastSyncedAt.Value.ToLocalTime():MMM d, h:mm tt}";
        ReadinessScore = supportSummary.ReadinessScore;
        ReadinessExplanationText = BuildReadinessExplanation(status, healthSummary);
    }

    private static string BuildReadinessExplanation(
        HealthSyncStatus status,
        HealthWeeklySummary? healthSummary)
    {
        if (!status.IsConnected)
            return "Estimated from cycle, nutrition logs, and today's plan.";

        if (healthSummary is null)
            return "Connected, waiting for weekly steps and sleep.";

        if (healthSummary.HasMovementData && healthSummary.HasSleepData)
            return "Synced steps and sleep are shaping today's plan.";

        if (healthSummary.HasMovementData)
            return "Synced steps are included; sleep is still estimated.";

        if (healthSummary.HasSleepData)
            return "Synced sleep is included; steps are still estimated.";

        return "Connected, but still estimating until weekly data arrives.";
    }

    partial void OnReadinessScoreChanged(int value)
    {
        OnPropertyChanged(nameof(ReadinessScoreText));
    }
}
