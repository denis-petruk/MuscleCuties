using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

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

    // The slider starts at its minimum and counts as an answer only once the person moves it.
    [ObservableProperty] private double _sleepHours;
    private bool _isSleepHoursAnswered;

    public DailyCheckInViewModel(
        IDailyCheckInUseCase checkIn,
        Action invalidateDashboard,
        Func<Task> onCompletedAsync)
    {
        _checkIn = checkIn;
        _isReadinessScoreDisplayEnabled = checkIn.PresentationSettings.IsReadinessScoreDisplayEnabled;
        _invalidateDashboard = invalidateDashboard;
        _onCompletedAsync = onCompletedAsync;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !IsCompleted && Answers.IsComplete);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
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
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Load failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Dashboard_Error_CheckInLoad";
        }
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
        SubmitCommand?.NotifyCanExecuteChanged();
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
