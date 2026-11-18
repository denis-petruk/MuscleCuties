using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHealthSyncService = MuscleCuties.Core.Services.Health.IHealthSyncService;
using MuscleCuties.UI.Services.Formatting;
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
    private readonly TimeProvider _clock;
    private DailyCheckInStatusRequest? _draftDay;
    private int _statusLoadVersion;
    private CancellationTokenSource? _submissionCancellation;
    [ObservableProperty] private double? _bodyWeight;
    [ObservableProperty] private int? _energyLevel;
    [ObservableProperty] private int? _planningFatigue;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool? _hasBloating;
    [ObservableProperty] private bool _showAdditionalDetails;
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
        ICurrentProfileUserIdProvider? userIdProvider = null,
        TimeProvider? clock = null)
    {
        _checkIn = checkIn;
        _isReadinessScoreDisplayEnabled = checkIn.PresentationSettings.IsReadinessScoreDisplayEnabled;
        _invalidateDashboard = invalidateDashboard;
        _onCompletedAsync = onCompletedAsync;
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        _clock = clock ?? TimeProvider.System;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !IsCompleted && Answers.IsReadyForPlanning);
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
        if (_healthSync is null || _userIdProvider is null || _draftDay is not { } day)
            return;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            if (userId <= 0 || !ReferenceEquals(_draftDay, day))
                return;
            // The only action that opens the Apple Health sheet on this screen.
            await _healthSync.SyncAsync(userId, _healthSync.DeviceHealthSource);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Connect failed ({ex.GetType().Name}).");
        }
        if (ReferenceEquals(_draftDay, day))
            await CheckIfAlreadyCompletedAsync();
    }

    private async Task NotNowHealthAsync()
    {
        ShowHealthRationale = false;
        if (_healthSync is null || _userIdProvider is null || _draftDay is not { } day)
            return;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            if (userId > 0 && ReferenceEquals(_draftDay, day))
                await _healthSync.DismissPromptAsync(userId);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Dismiss failed ({ex.GetType().Name}).");
        }
    }

    public string SleepDisplay => _isSleepHoursAnswered ? DisplayFormat.Hours(SleepHours) : "—";

    /// <summary>Whether all supplemental observations are also answered; planning uses CanSubmit.</summary>
    public bool AreAllAnswersProvided => Answers.IsComplete;

    private DailyCheckInAnswers Answers => new(
        _isSleepHoursAnswered ? SleepHours : null, PlanningSleepQuality, EnergyLevel, PlanningSoreness,
        PainLevel, HasBloating, BodyWeight, PlanningFatigue);
    public string EnergyDisplay => EnergyLevel is { } value ? $"{value} / 5" : "— / 5";
    public string FatigueDisplay => FormatPlanningRating(PlanningFatigue);
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
    public bool CanSubmit => !IsBusy && !IsCompleted && Answers.IsReadyForPlanning;

    /// <summary>
    /// D14: a disabled commit must explain what is missing instead of only going grey. The same
    /// string feeds the visible caption and the accessibility hint, so they cannot disagree.
    /// </summary>
    public string SubmitHint => Answers.IsReadyForPlanning
        ? UiTextCatalog.Get("Dashboard_CheckIn_SubmitHint")
        : UiTextCatalog.Get("Dashboard_CheckIn_SubmitPrerequisiteHint");

    public async Task CheckIfAlreadyCompletedAsync()
    {
        if (IsBusy)
            return;

        var loadVersion = ++_statusLoadVersion;
        var day = GetCurrentDay();
        if (_draftDay != day)
        {
            ResetDraft();
            _draftDay = day;
        }

        ErrorMessage = string.Empty;
        try
        {
            var status = await _checkIn.GetStatusAsync(day);
            if (loadVersion != _statusLoadVersion)
                return;

            var readiness = status.TodayReadiness;
            IsCompleted = readiness is { Status: "Available" };
            if (IsCompleted)
            {
                EnergyLevel = readiness?.Energy;
                PlanningFatigue = readiness?.Fatigue;
                PlanningSleepQuality = readiness?.SleepQuality;
                PlanningSoreness = readiness?.Soreness;
            }
            else
            {
                EnergyLevel ??= readiness?.Energy;
                PlanningFatigue ??= readiness?.Fatigue;
                PlanningSleepQuality ??= readiness?.SleepQuality;
                PlanningSoreness ??= readiness?.Soreness;
            }
            // Display only what was stored; stress is never derived from energy here.
            PlanningStress = readiness?.Stress;
            ReadinessScoreText = IsReadinessScoreDisplayEnabled && readiness is { Status: "Available", RawScore: { } }
                ? DisplayFormat.Decimal2(readiness!.RawScore!.Value)
                : string.Empty;
            ReadinessTierText = IsReadinessScoreDisplayEnabled ? BandLocalizationKey(readiness?.Band) : string.Empty;
            SessionSummaryText = string.Empty;
            OnPropertyChanged(nameof(EnergyDisplay));
            OnPropertyChanged(nameof(FatigueDisplay));
            OnPropertyChanged(nameof(PlanningSleepDisplay));
            OnPropertyChanged(nameof(PlanningSorenessDisplay));

            if (!IsCompleted && _healthSync is not null && _userIdProvider is not null)
            {
                var userId = await _userIdProvider.GetCurrentUserIdAsync();
                if (userId > 0 && loadVersion == _statusLoadVersion)
                {
                    var summary = await _healthSync.GetCachedWeeklySummaryAsync(userId);
                    if (summary is null && _healthSync.IsAvailable)
                    {
                        var healthStatus = await _healthSync.GetStatusAsync(userId);
                        if (loadVersion != _statusLoadVersion)
                            return;

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

                    if (loadVersion != _statusLoadVersion)
                        return;

                    // Never replace a value the user already entered (modal reopen).
                    if (summary?.LastNightSleepHours is > 0 && !_isSleepHoursAnswered)
                    {
                        SleepHours = Math.Clamp(summary.LastNightSleepHours.Value, 0.0, 16.0);
                        _isSleepHoursAnswered = true;
                        IsSleepAutoFilled = true;
                        SleepAutoFilledBadgeText = UiTextCatalog.Format(
                            "Dashboard_CheckIn_HealthKitSleepBadge",
                            DisplayFormat.Decimal1(SleepHours));
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
            if (loadVersion == _statusLoadVersion)
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

    [RelayCommand]
    private void ToggleAdditionalDetails() => ShowAdditionalDetails = !ShowAdditionalDetails;

    private async Task SubmitAsync()
    {
        if (IsBusy || IsCompleted)
            return;

        var day = GetCurrentDay();
        if (_draftDay != day)
        {
            await CheckIfAlreadyCompletedAsync();
            return;
        }

        var request = Answers.ToRequest(day.Date, day.TimeZoneId!,
            requireSupplementaryAnswers: false);
        if (request is null)
        {
            ErrorMessage = "Dashboard_CheckIn_IncompleteRequiredRatings";
            return;
        }

        IsBusy = true;
        ++_statusLoadVersion;
        using var cancellation = new CancellationTokenSource();
        _submissionCancellation = cancellation;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _checkIn.SubmitAsync(request, cancellation.Token);
            if (!ReferenceEquals(_submissionCancellation, cancellation))
                return;

            ReadinessScoreText = IsReadinessScoreDisplayEnabled && result.TodayReadiness?.RawScore is { } rawScore
                ? DisplayFormat.Decimal2(rawScore)
                : string.Empty;
            ReadinessTierText = IsReadinessScoreDisplayEnabled ? BandLocalizationKey(result.TodayReadiness?.Band) : string.Empty;
            SessionSummaryText = result.SessionSummary;
            IsCompleted = true;
            await _onCompletedAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Save failed ({ex.GetType().Name}).");
            if (ReferenceEquals(_submissionCancellation, cancellation))
                ErrorMessage = "Ui_Dashboard_Error_CheckInSave";
        }
        finally
        {
            if (ReferenceEquals(_submissionCancellation, cancellation))
            {
                _submissionCancellation = null;
                IsBusy = false;
            }
        }
    }

    private async Task SkipAsync()
    {
        _invalidateDashboard();
        await _onCompletedAsync();
    }

    private DailyCheckInStatusRequest GetCurrentDay()
    {
        var timeZone = _clock.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), timeZone);
        return new DailyCheckInStatusRequest(DateOnly.FromDateTime(localNow.DateTime), timeZone.Id);
    }

    private void ResetDraft()
    {
        IsCompleted = false;
        EnergyLevel = null;
        PlanningFatigue = null;
        PlanningSleepQuality = null;
        PlanningSorenessSlider = MinimumPlanningScaleValue;
        PlanningSoreness = null;
        PlanningStress = null;
        SleepHours = 0;
        _isSleepHoursAnswered = false;
        PainLevel = null;
        HasBloating = null;
        BodyWeight = null;
        ShowAdditionalDetails = false;
        IsSleepAutoFilled = false;
        SleepAutoFilledBadgeText = string.Empty;
        ShowHealthRationale = false;
        HasHealthKitSuggestions = false;
        SuggestedEnergyLevel = 0;
        SuggestedSleepQuality = 0;
        ReadinessScoreText = string.Empty;
        ReadinessTierText = string.Empty;
        SessionSummaryText = string.Empty;
        OnPropertyChanged(nameof(SleepDisplay));
        OnAnswerChanged();
    }

    internal void ResetForAccountChange()
    {
        ++_statusLoadVersion;
        _draftDay = null;
        var submission = _submissionCancellation;
        _submissionCancellation = null;
        submission?.Cancel();
        ResetDraft();
        ErrorMessage = string.Empty;
        IsBusy = false;
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

    partial void OnPlanningFatigueChanged(int? value)
    {
        OnPropertyChanged(nameof(FatigueDisplay));
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
