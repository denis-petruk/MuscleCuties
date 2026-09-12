using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DailyCheckInViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IHealthReadinessBridge _healthReadinessBridge;
    private readonly IReadinessRepository _readinessRepository;
    private readonly ICycleService _cycleService;
    private readonly IReadinessEngine _readinessEngine;
    private readonly IDailyCheckInNotificationService _notifications;
    private readonly IWorkoutService _workoutService;
    private readonly IAppPreloadService _preloadService;
    private readonly Func<Task> _onCompletedAsync;

    [ObservableProperty] private double _sleepHours = 7.0;
    [ObservableProperty] private int _energyLevel = 3;
    [ObservableProperty] private int _painLevel;
    [ObservableProperty] private bool _hasBloating;
    [ObservableProperty] private double? _bodyWeight;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string _readinessScoreText = string.Empty;
    [ObservableProperty] private string _readinessTierText = string.Empty;
    [ObservableProperty] private string _sessionSummaryText = string.Empty;

    public DailyCheckInViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        IReadinessRepository readinessRepository,
        IHealthReadinessBridge healthReadinessBridge,
        ICycleService cycleService,
        IReadinessEngine readinessEngine,
        IDailyCheckInNotificationService notifications,
        IWorkoutService workoutService,
        IAppPreloadService preloadService,
        Func<Task> onCompletedAsync)
    {
        _authService = authService;
        _userRepository = userRepository;
        _readinessRepository = readinessRepository;
        _healthReadinessBridge = healthReadinessBridge;
        _cycleService = cycleService;
        _readinessEngine = readinessEngine;
        _notifications = notifications;
        _workoutService = workoutService;
        _preloadService = preloadService;
        _onCompletedAsync = onCompletedAsync;

        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !IsCompleted);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
    }

    public string SleepDisplay => $"{SleepHours:F1}h";
    public string EnergyDisplay => $"{EnergyLevel} / 5";

    public string PainDisplay => PainLevel switch
    {
        0 => "None",
        1 => "Mild",
        2 => "Moderate",
        3 => "Severe",
        _ => "None"
    };

    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand SkipCommand { get; }

    public async Task CheckIfAlreadyCompletedAsync()
    {
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var existing = await _readinessRepository.GetForAsync(userId, today);

            if (existing is not null)
                IsCompleted = true;
        }
        catch
        {
        }
    }

    private async Task SubmitAsync()
    {
        if (IsBusy || IsCompleted)
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);

            var log = new DailyReadinessLog
            {
                UserId = userId,
                Date = today,
                SleepHours = SleepHours,
                Sleep3dAvg = SleepHours,
                StepsYesterday = 8000,
                Steps7dAvg = 8000,
                Energy = EnergyLevel,
                Pain = PainLevel,
                Bloating = HasBloating,
                WeightKg = BodyWeight,
                Phase = phase.ToString()
            };

            await _healthReadinessBridge.EnrichAsync(userId, log);
            var profile = await _userRepository.GetProfileAsync(userId);
            var baselines = AdaptiveProfileMapper.ParseBaselines(profile?.PhaseBaselinesJson);
            var readiness = _readinessEngine.Compute(new DailyInputs(
                log.Date,
                log.SleepHours,
                log.Sleep3dAvg,
                log.StepsYesterday,
                log.Steps7dAvg,
                log.Energy,
                log.Pain,
                log.Bloating,
                log.WeightKg), phase, baselines);
            log.ReadinessScore = readiness.Score;
            log.ReadinessTier = (int)readiness.Tier;
            await _readinessRepository.SaveInputsAsync(log);

            await _workoutService.RegenerateActivePlanAsync(userId, phase);
            var workout = await _workoutService.GetTodaysSummaryAsync(userId, phase, DateTime.Today);

            ReadinessScoreText = readiness.Score.ToString();
            ReadinessTierText = readiness.Tier.ToString();
            SessionSummaryText = workout.Title;

            IsCompleted = true;
            _preloadService.InvalidateDashboard();
            _preloadService.InvalidateWorkout();
            await _onCompletedAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SkipAsync()
    {
        _preloadService.InvalidateDashboard();
        await _onCompletedAsync();
    }

    partial void OnSleepHoursChanged(double value)
    {
        SleepHours = Math.Clamp(value, 0, 14);
        OnPropertyChanged(nameof(SleepDisplay));
    }

    partial void OnEnergyLevelChanged(int value)
    {
        EnergyLevel = Math.Clamp(value, 1, 5);
        OnPropertyChanged(nameof(EnergyDisplay));
    }

    partial void OnPainLevelChanged(int value)
    {
        PainLevel = Math.Clamp(value, 0, 3);
        OnPropertyChanged(nameof(PainDisplay));
    }

    partial void OnIsBusyChanged(bool value)
    {
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCompletedChanged(bool value)
    {
        SubmitCommand.NotifyCanExecuteChanged();
    }
}
