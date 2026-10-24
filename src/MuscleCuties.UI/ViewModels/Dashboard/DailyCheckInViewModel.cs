using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private bool _hasBloating;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private int _painLevel;
    [ObservableProperty] private int? _planningSleepQuality;
    [ObservableProperty] private int? _planningSoreness;
    [ObservableProperty] private int? _planningStress;
    [ObservableProperty] private string _readinessScoreText = string.Empty;
    [ObservableProperty] private string _readinessTierText = string.Empty;
    [ObservableProperty] private string _sessionSummaryText = string.Empty;

    [ObservableProperty] private double _sleepHours = 7.0;

    public DailyCheckInViewModel(
        IDailyCheckInUseCase checkIn,
        Action invalidateDashboard,
        Func<Task> onCompletedAsync)
    {
        _checkIn = checkIn;
        _isReadinessScoreDisplayEnabled = checkIn.PresentationSettings.IsReadinessScoreDisplayEnabled;
        _invalidateDashboard = invalidateDashboard;
        _onCompletedAsync = onCompletedAsync;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !IsCompleted);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
    }

    public string SleepDisplay => $"{SleepHours:F1}h";
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
            var status = await _checkIn.GetStatusAsync(
                new DailyCheckInStatusRequest(DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local.Id));
            var readiness = status.TodayReadiness;
            EnergyLevel = readiness?.Energy;
            PlanningSleepQuality = readiness?.SleepQuality;
            PlanningSoreness = readiness?.Soreness;
            PlanningStress = readiness?.Stress;
            IsCompleted = readiness is { Status: "Available" };
            ReadinessScoreText = IsReadinessScoreDisplayEnabled && readiness is { Status: "Available", RawScore: { } }
                ? readiness!.RawScore!.Value.ToString("0.##", CultureInfo.CurrentCulture)
                : string.Empty;
            ReadinessTierText = IsReadinessScoreDisplayEnabled ? BandLocalizationKey(readiness?.Band) : string.Empty;
            SessionSummaryText = string.Empty;
            OnPropertyChanged(nameof(EnergyDisplay));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DailyCheckInViewModel] Load failed ({ex.GetType().Name}).");
            ErrorMessage = "Could not load today's check-in. Please try again.";
        }
    }

    private async Task SubmitAsync()
    {
        if (IsBusy || IsCompleted)
            return;

        if (EnergyLevel is not (>= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue) ||
            PlanningSleepQuality is not (>= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue) ||
            PlanningSoreness is not (>= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue) ||
            PlanningStress is not (>= MinimumPlanningScaleValue and <= MaximumPlanningScaleValue))
        {
            ErrorMessage = "Dashboard_CheckIn_IncompleteAllFourRatings";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _checkIn.SubmitAsync(new SubmitDailyCheckInRequest(
                DateOnly.FromDateTime(DateTime.Today), SleepHours, EnergyLevel.Value,
                PainLevel, HasBloating, BodyWeight, PlanningSleepQuality, PlanningSoreness, PlanningStress,
                TimeZoneInfo.Local.Id));
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
            ErrorMessage = "Could not save your check-in. Please try again.";
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
        SleepHours = Math.Clamp(value, 0, 14);
        OnPropertyChanged(nameof(SleepDisplay));
    }

    partial void OnEnergyLevelChanged(int? value)
    {
        OnPropertyChanged(nameof(EnergyDisplay));
    }

    partial void OnPlanningSleepQualityChanged(int? value) => OnPropertyChanged(nameof(PlanningSleepDisplay));
    partial void OnPlanningSorenessChanged(int? value) => OnPropertyChanged(nameof(PlanningSorenessDisplay));
    partial void OnPlanningStressChanged(int? value) => OnPropertyChanged(nameof(PlanningStressDisplay));

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
