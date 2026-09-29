using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DailyCheckInViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHealthReadinessBridge _healthReadinessBridge;
    private readonly ICycleService _cycleService;
    private readonly IReadinessEngine _readinessEngine;
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
    [ObservableProperty] private string _errorMessage = string.Empty;

    public DailyCheckInViewModel(
        IAuthService authService,
        IServiceScopeFactory scopeFactory,
        IHealthReadinessBridge healthReadinessBridge,
        ICycleService cycleService,
        IReadinessEngine readinessEngine,
        IWorkoutService workoutService,
        IAppPreloadService preloadService,
        Func<Task> onCompletedAsync)
    {
        _authService = authService;
        _scopeFactory = scopeFactory;
        _healthReadinessBridge = healthReadinessBridge;
        _cycleService = cycleService;
        _readinessEngine = readinessEngine;
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

    public bool HasSessionSummary => !string.IsNullOrWhiteSpace(SessionSummaryText);

    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand SkipCommand { get; }

    public async Task CheckIfAlreadyCompletedAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var existing = await scope.ServiceProvider.GetRequiredService<IDailyCheckInDataService>()
                .GetCheckInAsync(userId, today);
            if (existing is null)
            {
                IsCompleted = false;
                ReadinessScoreText = string.Empty;
                ReadinessTierText = string.Empty;
                SessionSummaryText = string.Empty;
                return;
            }

            ReadinessScoreText = existing.ReadinessScore is >= 0 and <= 100
                ? existing.ReadinessScore.ToString()
                : "—";
            ReadinessTierText = Enum.IsDefined(typeof(ReadinessTier), existing.ReadinessTier)
                ? ((ReadinessTier)existing.ReadinessTier).ToString()
                : "Check-in complete";
            SessionSummaryText = string.Empty;
            IsCompleted = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load today's check-in. Please try again.";
        }
    }

    private async Task SubmitAsync()
    {
        if (IsBusy || IsCompleted)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        var persisted = false;
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
                Sleep3dAvg = 0,
                StepsYesterday = 0,
                Steps7dAvg = 0,
                Energy = EnergyLevel,
                Pain = PainLevel,
                Bloating = HasBloating,
                WeightKg = BodyWeight,
                Phase = phase.ToString()
            };

            await _healthReadinessBridge.EnrichAsync(userId, log);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var checkInData = scope.ServiceProvider.GetRequiredService<IDailyCheckInDataService>();
            var baselines = await checkInData.GetPhaseBaselinesAsync(userId);
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
            await checkInData.SaveAsync(log);
            persisted = true;

            ReadinessScoreText = readiness.Score.ToString();
            ReadinessTierText = readiness.Tier.ToString();
            _preloadService.InvalidateDashboard();
            _preloadService.InvalidateWorkout();

            await _workoutService.RegenerateActivePlanAsync(userId, phase);
            var workout = await _workoutService.GetTodaysSummaryAsync(userId, phase, DateTime.Today);
            SessionSummaryText = workout.Title;
            IsCompleted = true;
            await _onCompletedAsync();
        }
        catch (Exception)
        {
            if (persisted)
            {
                IsCompleted = true;
                ErrorMessage = "Check-in saved. Return to Today to continue; the plan may need to refresh.";
            }
            else
            {
                ErrorMessage = "Could not save your check-in. Please try again.";
            }
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

    partial void OnSessionSummaryTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSessionSummary));
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
