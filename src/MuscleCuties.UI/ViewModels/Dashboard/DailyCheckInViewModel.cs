using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UI.ViewModels.Dashboard;

public partial class DailyCheckInViewModel : ObservableObject
{
    private readonly IDailyCheckInUseCase _checkIn;
    private readonly Action _invalidateDashboard;
    private readonly Func<Task> _onCompletedAsync;
    [ObservableProperty] private double? _bodyWeight;
    [ObservableProperty] private int _energyLevel = 3;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasBloating;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private int _painLevel;
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
        _invalidateDashboard = invalidateDashboard;
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

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in CheckIn XAML / view model contract.")]
    public bool HasApprovedScorePolicy => false;

    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand SkipCommand { get; }

    public async Task CheckIfAlreadyCompletedAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var status = await _checkIn.GetStatusAsync(
                new DailyCheckInStatusRequest(DateOnly.FromDateTime(DateTime.Today)));
            IsCompleted = status.IsCompleted;
            ReadinessScoreText = status.ScoreText;
            ReadinessTierText = status.TierText;
            SessionSummaryText = string.Empty;
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

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _checkIn.SubmitAsync(new SubmitDailyCheckInRequest(
                DateOnly.FromDateTime(DateTime.Today), SleepHours, EnergyLevel,
                PainLevel, HasBloating, BodyWeight));
            ReadinessScoreText = result.ReadinessScore.ToString(CultureInfo.InvariantCulture);
            ReadinessTierText = result.ReadinessTier;
            SessionSummaryText = result.SessionSummary;
            IsCompleted = true;
            if (result.PlanRefreshFailed)
                ErrorMessage = "Check-in saved. Return to Today to continue; the plan may need to refresh.";
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
