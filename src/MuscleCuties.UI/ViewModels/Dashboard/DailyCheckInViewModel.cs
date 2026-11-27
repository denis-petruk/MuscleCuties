using System.Diagnostics.CodeAnalysis;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using IHealthSyncService = MuscleCuties.Core.Services.Health.IHealthSyncService;

namespace MuscleCuties.UI.ViewModels.Dashboard;

/// <summary>The daily check-in sheet: how she slept and feels today, answered once a day so readiness and the day's plan</summary>
/// can follow from it.</summary>
public partial class DailyCheckInViewModel : ObservableObject
{
    private const int MinimumPlanningScaleValue = 1;
    private const int MaximumPlanningScaleValue = 5;

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

    /// <summary>Takes the check-in use case and the callbacks it reports to.</summary>
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

    /// <summary>Connects the health data source.</summary>
    public AsyncRelayCommand ConnectHealthCommand { get; }
    /// <summary>Dismisses the health suggestion.</summary>
    public AsyncRelayCommand NotNowHealthCommand { get; }

    /// <summary>Connects the health store and reloads the day, so sleep and activity she would otherwise type can come from</summary>
    /// the device instead.</summary>
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

    /// <summary>Declines health access for now: the rationale closes and the check-in carries on with the typed answers</summary>
    /// alone.</summary>
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

    /// <summary>Hours slept, or a dash until she actually answers the sleep question.</summary>
    public string SleepDisplay => _isSleepHoursAnswered ? DisplayFormat.Hours(SleepHours) : "—";

    /// <summary>Whether all supplemental observations are also answered; planning uses CanSubmit.</summary>
    public bool AreAllAnswersProvided => Answers.IsComplete;

    /// <summary>The form as the answer set the use case validates; a sleep figure she never entered stays unset rather than</summary>
    /// counting as zero.</summary>
    private DailyCheckInAnswers Answers => new(
        _isSleepHoursAnswered ? SleepHours : null, PlanningSleepQuality, EnergyLevel, PlanningSoreness,
        PainLevel, HasBloating, BodyWeight, PlanningFatigue);
    /// <summary>Energy out of five, with a dash while it is unanswered.</summary>
    public string EnergyDisplay => EnergyLevel is { } value ? $"{value} / 5" : "— / 5";
    /// <summary>The fatigue rating on its scale, with a dash when it was not given.</summary>
    public string FatigueDisplay => FormatPlanningRating(PlanningFatigue);
    /// <summary>The sleep-quality rating on the same scale.</summary>
    public string PlanningSleepDisplay => FormatPlanningRating(PlanningSleepQuality);
    /// <summary>The soreness rating on the same scale.</summary>
    public string PlanningSorenessDisplay => FormatPlanningRating(PlanningSoreness);
    /// <summary>The stress rating on the same scale.</summary>
    public string PlanningStressDisplay => FormatPlanningRating(PlanningStress);
    /// <summary>True when the readiness score is shown.</summary>
    public bool IsReadinessScoreDisplayEnabled => _isReadinessScoreDisplayEnabled;

    /// <summary>Pain as a word, because a bare number would read as a measurement rather than her own report.</summary>
    public string PainDisplay => PainLevel switch
    {
        0 => "None",
        1 => "Mild",
        2 => "Moderate",
        3 => "Severe",
        _ => "—"
    };

    /// <summary>True when a session summary line is shown.</summary>
    public bool HasSessionSummary => !string.IsNullOrWhiteSpace(SessionSummaryText);

    /// <summary>Submits the check-in.</summary>
    public AsyncRelayCommand SubmitCommand { get; }
    /// <summary>Skips the check-in.</summary>
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

    /// <summary>Reads today's check-in and, when there already is one, closes the sheet as answered instead of asking the</summary>
    /// same questions again.</summary>
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

    /// <summary>Fills the answers from the health data.</summary>
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

    /// <summary>Shows or hides the additional details.</summary>
    [RelayCommand]
    private void ToggleAdditionalDetails() => ShowAdditionalDetails = !ShowAdditionalDetails;

    /// <summary>Submits the answers once and closes on success; a refusal keeps everything typed so none of it is lost.</summary>
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

    /// <summary>Records that today was declined, so the sheet does not reopen, and tolerates a store that refuses the note.</summary>
    private async Task SkipAsync()
    {
        _invalidateDashboard();
        await _onCompletedAsync();
    }

    /// <summary>The day the sheet is currently about.</summary>
    private DailyCheckInStatusRequest GetCurrentDay()
    {
        var timeZone = _clock.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), timeZone);
        return new DailyCheckInStatusRequest(DateOnly.FromDateTime(localNow.DateTime), timeZone.Id);
    }

    /// <summary>Clears the answers so the next day starts blank.</summary>
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

    /// <summary>Clears the sheet when the signed-in account changes, so one account's answers can never be saved into</summary>
    /// another's day.</summary>
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

    /// <summary>Refreshes the sleep guidance when the hours change.</summary>
    partial void OnSleepHoursChanged(double value)
    {
        SleepHours = Math.Clamp(value, DailyCheckInAnswers.MinimumSleepHours, DailyCheckInAnswers.MaximumSleepHours);
        _isSleepHoursAnswered = true;
        // Any change here is a manual edit; the auto-fill block sets the flag again right after it assigns the value.
        IsSleepAutoFilled = false;
        OnPropertyChanged(nameof(SleepDisplay));
        OnAnswerChanged();
    }

    /// <summary>Refreshes the answers when the energy level changes.</summary>
    partial void OnEnergyLevelChanged(int? value)
    {
        OnPropertyChanged(nameof(EnergyDisplay));
        OnAnswerChanged();
    }

    /// <summary>Refreshes the answers when the planning fatigue changes.</summary>
    partial void OnPlanningFatigueChanged(int? value)
    {
        OnPropertyChanged(nameof(FatigueDisplay));
        OnAnswerChanged();
    }

    /// <summary>Refreshes the answers when the planning sleep quality changes.</summary>
    partial void OnPlanningSleepQualityChanged(int? value)
    {
        OnPropertyChanged(nameof(PlanningSleepDisplay));
        OnAnswerChanged();
    }

    /// <summary>Refreshes the answers when the planning soreness changes.</summary>
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

    /// <summary>Maps the soreness slider onto the answer scale.</summary>
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

    /// <summary>Refreshes the stress display when the answer changes.</summary>
    partial void OnPlanningStressChanged(int? value) => OnPropertyChanged(nameof(PlanningStressDisplay));

    /// <summary>Refreshes the answers when the pain level changes.</summary>
    partial void OnPainLevelChanged(int? value)
    {
        if (value is { } level)
            PainLevel = Math.Clamp(level, DailyCheckInAnswers.MinimumPainLevel, DailyCheckInAnswers.MaximumPainLevel);
        OnPropertyChanged(nameof(PainDisplay));
        OnAnswerChanged();
    }

    /// <summary>Refreshes the answers when the bloating answer changes.</summary>
    partial void OnHasBloatingChanged(bool? value) => OnAnswerChanged();

    /// <summary>Refreshes the submit state after an answer changes.</summary>
    private void OnAnswerChanged()
    {
        OnPropertyChanged(nameof(AreAllAnswersProvided));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitHint));
        SubmitCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the summary flag when the text changes.</summary>
    partial void OnSessionSummaryTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSessionSummary));
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitHint));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the completion state when the check-in completes.</summary>
    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The catalog key for a readiness band.</summary>
    private static string BandLocalizationKey(string? band) => band switch
    {
        "FeelingReady" => "Dashboard_Readiness_Band_FeelingReady",
        "ConsiderLighterSession" => "Dashboard_Readiness_Band_ConsiderLighterSession",
        "ConsiderEasierDay" => "Dashboard_Readiness_Band_ConsiderEasierDay",
        _ => string.Empty
    };

    /// <summary>The rating shown for a planning answer.</summary>
    private static string FormatPlanningRating(int? value) =>
        value is >= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue ? $"{value} / 5" : "— / 5";
}
