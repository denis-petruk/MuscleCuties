using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHealthSyncService = MuscleCuties.Core.Services.Health.IHealthSyncService;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.ViewModels.Dashboard;

public partial class DailyCheckInViewModel : ObservableObject
{
    private const int MinimumPlanningScaleValue = 1;
    private const int MaximumPlanningScaleValue = 5;
    private readonly IReadOnlyList<int> _planningScaleOptions =
        Enumerable.Range(MinimumPlanningScaleValue, MaximumPlanningScaleValue - MinimumPlanningScaleValue + 1).ToArray();

    private readonly IDailyCheckInUseCase _checkIn;
    private readonly Action _invalidateDashboard;
    private readonly Func<Task> _onCompletedAsync;
    private readonly bool _isReadinessScoreDisplayEnabled;
    private readonly IHealthSyncService? _healthSync;
    private readonly ICurrentProfileUserIdProvider? _userIdProvider;
    [ObservableProperty] private double? _bodyWeight;
    [ObservableProperty] private int? _energyLevel;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool? _hasBloating;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private int? _painLevel;
    [ObservableProperty] private int? _planningSleepQuality;
    [ObservableProperty] private int? _planningSoreness;
    [ObservableProperty] private double _planningSorenessSlider = MinimumPlanningScaleValue;
    [ObservableProperty] private int? _planningStress;
    [ObservableProperty] private string _readinessScoreText = string.Empty;
    [ObservableProperty] private string _readinessTierText = string.Empty;
    [ObservableProperty] private string _sessionSummaryText = string.Empty;
    [ObservableProperty] private bool _isSleepAutoFilled;
    [ObservableProperty] private string _sleepAutoFilledBadgeText = string.Empty;
    [ObservableProperty] private bool _hasHealthKitSuggestions;
    [ObservableProperty] private int _suggestedEnergyLevel;
    [ObservableProperty] private int _suggestedSleepQuality;

    // The slider starts at its minimum and counts as an answer only once the person moves it.
    [ObservableProperty] private double _sleepHours;
    private bool _isSleepHoursAnswered;

    public DailyCheckInViewModel(
        IDailyCheckInUseCase checkIn,
        Action invalidateDashboard,
        Func<Task> onCompletedAsync,
        IHealthSyncService? healthSync = null,
        ICurrentProfileUserIdProvider? userIdProvider = null)
    {
        _checkIn = checkIn;
        _isReadinessScoreDisplayEnabled = checkIn.PresentationSettings.IsReadinessScoreDisplayEnabled;
        _invalidateDashboard = invalidateDashboard;
        _onCompletedAsync = onCompletedAsync;
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !IsCompleted && Answers.IsComplete);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
        ConnectHealthCommand = new AsyncRelayCommand(ConnectHealthAsync);
        NotNowHealthCommand = new AsyncRelayCommand(NotNowHealthAsync);
    }

    [ObservableProperty] private bool _showHealthRationale;

    public AsyncRelayCommand ConnectHealthCommand { get; }
    public AsyncRelayCommand NotNowHealthCommand { get; }

    private async Task ConnectHealthAsync()
    {
        ShowHealthRationale = false;
        if (_healthSync is null || _userIdProvider is null) return;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            // The only action that opens the Apple Health sheet on this screen.
            await _healthSync.SyncAsync(userId, _healthSync.DeviceHealthSource);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Connect failed ({ex.GetType().Name}).");
        }
        await CheckIfAlreadyCompletedAsync();
    }

    private async Task NotNowHealthAsync()
    {
        ShowHealthRationale = false;
        if (_healthSync is null || _userIdProvider is null) return;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            await _healthSync.DismissPromptAsync(userId);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Dismiss failed ({ex.GetType().Name}).");
        }
    }

    public string SleepDisplay => _isSleepHoursAnswered ? $"{SleepHours:F1}h" : "—";

    /// <summary>True once every required check-in question has an explicit answer.</summary>
    public bool AreAllAnswersProvided => Answers.IsComplete;

    private DailyCheckInAnswers Answers => new(
        _isSleepHoursAnswered ? SleepHours : null, PlanningSleepQuality, EnergyLevel, PlanningSoreness,
        PainLevel, HasBloating, BodyWeight);
    public string EnergyDisplay => EnergyLevel is { } value ? $"{value} / 5" : "— / 5";
    public string PlanningSleepDisplay => FormatPlanningRating(PlanningSleepQuality);
    public string PlanningSorenessDisplay => FormatPlanningRating(PlanningSoreness);
    public string PlanningStressDisplay => FormatPlanningRating(PlanningStress);
    public IReadOnlyList<int> PlanningScaleOptions => _planningScaleOptions;
    public bool IsReadinessScoreDisplayEnabled => _isReadinessScoreDisplayEnabled;

    public string PainDisplay => PainLevel switch
    {
        0 => "None",
        1 => "Mild",
        2 => "Moderate",
        3 => "Severe",
        _ => "—"
    };

    public bool HasSessionSummary => !string.IsNullOrWhiteSpace(SessionSummaryText);

    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand SkipCommand { get; }

    /// <summary>True when the commit action can run; false names a prerequisite in <see cref="SubmitHint" />.</summary>
    public bool CanSubmit => !IsBusy && !IsCompleted && Answers.IsComplete;

    /// <summary>
    /// D14: a disabled commit must explain what is missing instead of only going grey. The same
    /// string feeds the visible caption and the accessibility hint, so they cannot disagree.
    /// </summary>
    public string SubmitHint => Answers.IsComplete
        ? UiTextCatalog.Get("Dashboard_CheckIn_SubmitHint")
        : UiTextCatalog.Get("Dashboard_CheckIn_SubmitPrerequisiteHint");

    public async Task CheckIfAlreadyCompletedAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var status = await _checkIn.GetStatusAsync(
                new DailyCheckInStatusRequest(DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local.Id));
            var readiness = status.TodayReadiness;
            EnergyLevel = readiness?.Energy;
            PlanningSleepQuality = readiness?.SleepQuality;
            PlanningSoreness = readiness?.Soreness;
            // Display only what was stored; stress is never derived from energy here.
            PlanningStress = readiness?.Stress;
            IsCompleted = readiness is { Status: "Available" };
            ReadinessScoreText = IsReadinessScoreDisplayEnabled && readiness is { Status: "Available", RawScore: { } }
                ? readiness!.RawScore!.Value.ToString("0.##", CultureInfo.CurrentCulture)
                : string.Empty;
            ReadinessTierText = IsReadinessScoreDisplayEnabled ? BandLocalizationKey(readiness?.Band) : string.Empty;
            SessionSummaryText = string.Empty;
            OnPropertyChanged(nameof(EnergyDisplay));
            OnPropertyChanged(nameof(PlanningSleepDisplay));
            OnPropertyChanged(nameof(PlanningSorenessDisplay));

            if (!IsCompleted && _healthSync is not null && _userIdProvider is not null)
            {
                var userId = await _userIdProvider.GetCurrentUserIdAsync();
                if (userId > 0)
                {
                    var summary = await _healthSync.GetCachedWeeklySummaryAsync(userId);
                    if (summary is null && _healthSync.IsAvailable)
                    {
                        var healthStatus = await _healthSync.GetStatusAsync(userId);
                        if (healthStatus.IsConnected)
                        {
                            // Already authorized earlier: refresh silently. This never shows the system sheet.
                            try
                            {
                                var syncResult = await _healthSync.SyncAsync(userId, _healthSync.DeviceHealthSource);
                                summary = syncResult.Summary ?? await _healthSync.GetCachedWeeklySummaryAsync(userId);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine($"[DailyCheckInViewModel] Contextual health sync failed ({ex.GetType().Name}).");
                            }
                        }
                        else if (!healthStatus.PromptDismissed)
                        {
                            // First time: explain first; the system sheet only follows an explicit Connect tap.
                            ShowHealthRationale = true;
                        }
                    }

                    // Never replace a value the user already entered (modal reopen).
                    if (summary?.LastNightSleepHours is > 0 && !_isSleepHoursAnswered)
                    {
                        SleepHours = Math.Clamp(summary.LastNightSleepHours.Value, 0.0, 16.0);
                        _isSleepHoursAnswered = true;
                        IsSleepAutoFilled = true;
                        SleepAutoFilledBadgeText = UiTextCatalog.Format(
                            "Dashboard_CheckIn_HealthKitSleepBadge",
                            SleepHours.ToString("F1", CultureInfo.CurrentCulture));
                        OnPropertyChanged(nameof(SleepDisplay));
                        OnAnswerChanged();
                    }

                    if (summary is not null && (summary.LastNightSleepHours is > 0 || summary.RestingHeartRate > 0 || summary.HrvScore > 0))
                    {
                        // Unknown sleep is not "0 hours": without a sleep sample no sleep-quality rating is
                        // suggested and sleep never counts toward the energy suggestion.
                        var sleepKnown = summary.LastNightSleepHours is > 0;
                        var sleep = summary.LastNightSleepHours ?? 0.0;
                        var rhr = summary.RestingHeartRate;
                        var hrv = summary.HrvScore;

                        int sleepQuality = 0;
                        if (sleepKnown)
                        {
                            if (sleep >= 8.0 && ((rhr > 0 && rhr <= 60) || hrv >= 60))
                                sleepQuality = 5;
                            else if (sleep >= 7.0)
                                sleepQuality = 4;
                            else if (sleep >= 6.0)
                                sleepQuality = 3;
                            else if (sleep >= 4.5)
                                sleepQuality = 2;
                            else
                                sleepQuality = 1;
                        }

                        int energyLevel;
                        if (sleepKnown && sleep >= 7.5 && ((rhr > 0 && rhr <= 65) || hrv >= 50))
                            energyLevel = 4;
                        else if ((sleepKnown && sleep < 6.0) || rhr > 75 || (hrv > 0 && hrv < 30))
                            energyLevel = 2;
                        else
                            energyLevel = 3;

                        SuggestedEnergyLevel = energyLevel;
                        SuggestedSleepQuality = sleepQuality;
                        HasHealthKitSuggestions = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Load failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Dashboard_Error_CheckInLoad";
        }
    }

    [RelayCommand]
    private void SuggestFromAppleHealth()
    {
        EnergyLevel = SuggestedEnergyLevel;
        // 0 means "no sleep sample, nothing to suggest"; leave the user's own rating untouched.
        if (SuggestedSleepQuality > 0)
            PlanningSleepQuality = SuggestedSleepQuality;
        OnPropertyChanged(nameof(EnergyDisplay));
        OnPropertyChanged(nameof(PlanningSleepDisplay));
        OnAnswerChanged();
    }

    private async Task SubmitAsync()
    {
        if (IsBusy || IsCompleted)
            return;

        var request = Answers.ToRequest(DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local.Id);
        if (request is null)
        {
            ErrorMessage = "Dashboard_CheckIn_IncompleteAllFourRatings";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _checkIn.SubmitAsync(request);
            ReadinessScoreText = IsReadinessScoreDisplayEnabled && result.TodayReadiness?.RawScore is { } rawScore
                ? rawScore.ToString("0.##", CultureInfo.CurrentCulture)
                : string.Empty;
            ReadinessTierText = IsReadinessScoreDisplayEnabled ? BandLocalizationKey(result.TodayReadiness?.Band) : string.Empty;
            SessionSummaryText = result.SessionSummary;
            IsCompleted = true;
            await _onCompletedAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Save failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Dashboard_Error_CheckInSave";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SkipAsync()
    {
        _invalidateDashboard();
        await _onCompletedAsync();
    }

    partial void OnSleepHoursChanged(double value)
    {
        SleepHours = Math.Clamp(value, DailyCheckInAnswers.MinimumSleepHours, DailyCheckInAnswers.MaximumSleepHours);
        _isSleepHoursAnswered = true;
        // Any change here is a manual edit; the auto-fill block sets the flag again right after it assigns the value.
        IsSleepAutoFilled = false;
        OnPropertyChanged(nameof(SleepDisplay));
        OnAnswerChanged();
    }

    partial void OnEnergyLevelChanged(int? value)
    {
        OnPropertyChanged(nameof(EnergyDisplay));
        OnAnswerChanged();
    }

    partial void OnPlanningSleepQualityChanged(int? value)
    {
        OnPropertyChanged(nameof(PlanningSleepDisplay));
        OnAnswerChanged();
    }

    partial void OnPlanningSorenessChanged(int? value)
    {
        if (value is >= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue &&
            Math.Abs(PlanningSorenessSlider - value.Value) > 0.001)
        {
            PlanningSorenessSlider = value.Value;
        }

        OnPropertyChanged(nameof(PlanningSorenessDisplay));
        OnAnswerChanged();
    }

    partial void OnPlanningSorenessSliderChanged(double value)
    {
        var clamped = Math.Clamp(value, MinimumPlanningScaleValue, MaximumPlanningScaleValue);
        var rounded = (int)Math.Round(clamped, MidpointRounding.AwayFromZero);
        if (PlanningSoreness != rounded)
        {
            PlanningSoreness = rounded;
        }

        OnPropertyChanged(nameof(PlanningSorenessDisplay));
    }

    partial void OnPlanningStressChanged(int? value) => OnPropertyChanged(nameof(PlanningStressDisplay));

    partial void OnPainLevelChanged(int? value)
    {
        if (value is { } level)
            PainLevel = Math.Clamp(level, DailyCheckInAnswers.MinimumPainLevel, DailyCheckInAnswers.MaximumPainLevel);
        OnPropertyChanged(nameof(PainDisplay));
        OnAnswerChanged();
    }

    partial void OnHasBloatingChanged(bool? value) => OnAnswerChanged();

    private void OnAnswerChanged()
    {
        OnPropertyChanged(nameof(AreAllAnswersProvided));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitHint));
        SubmitCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSessionSummaryTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSessionSummary));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitHint));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private static string BandLocalizationKey(string? band) => band switch
    {
        "FeelingReady" => "Dashboard_Readiness_Band_FeelingReady",
        "ConsiderLighterSession" => "Dashboard_Readiness_Band_ConsiderLighterSession",
        "ConsiderEasierDay" => "Dashboard_Readiness_Band_ConsiderEasierDay",
        _ => string.Empty
    };

    private static string FormatPlanningRating(int? value) =>
        value is >= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue ? $"{value} / 5" : "— / 5";
}
