using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class WorkoutViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly IWorkoutRepository _workoutRepository;
    private List<WorkoutItem> _allWorkouts = new();
    private CyclePhase _currentPhase;

    // Week plan
    [ObservableProperty] private WorkoutPlan? _activePlan;
    [ObservableProperty] private List<WorkoutDay> _workoutDays = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    // Today state
    [ObservableProperty] private bool _isActiveDay;
    [ObservableProperty] private bool _isActiveRecovery;
    [ObservableProperty] private bool _isPassiveRecovery;
    [ObservableProperty] private string _todayTitle = string.Empty;
    [ObservableProperty] private string _todayBodyCopy = string.Empty;
    [ObservableProperty] private string _todayIntensity = string.Empty;
    [ObservableProperty] private string _nextActiveDayName = string.Empty;
    [ObservableProperty] private string _restDayTip = string.Empty;
    [ObservableProperty] private ObservableCollection<ExerciseDetailItem> _todaysExercises = new();

    // Swap
    [ObservableProperty] private bool _isSwapSheetVisible;
    [ObservableProperty] private ObservableCollection<WorkoutItem> _alternativeWorkouts = new();
    private WorkoutDay? _todaysWorkoutDay;

    public AsyncRelayCommand LoadDataCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public RelayCommand<WorkoutItem> OpenDetailCommand { get; }
    public RelayCommand ShowSwapCommand { get; }
    public RelayCommand HideSwapCommand { get; }
    public RelayCommand<WorkoutItem> SelectAlternativeCommand { get; }
    public RelayCommand StartWorkoutCommand { get; }

    public WorkoutViewModel(
        IAuthService authService,
        ICycleService cycleService,
        IWorkoutRepository workoutRepository,
        Action<int>? openDetail = null)
    {
        _authService = authService;
        _cycleService = cycleService;
        _workoutRepository = workoutRepository;

        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        OpenDetailCommand = new RelayCommand<WorkoutItem>(item =>
        {
            if (item is not null) openDetail?.Invoke(item.WorkoutDayId);
        });
        ShowSwapCommand = new RelayCommand(ShowSwap);
        HideSwapCommand = new RelayCommand(() => IsSwapSheetVisible = false);
        SelectAlternativeCommand = new RelayCommand<WorkoutItem>(SelectAlternative);
        StartWorkoutCommand = new RelayCommand(() =>
        {
            if (_todaysWorkoutDay is not null) openDetail?.Invoke(_todaysWorkoutDay.Id);
        });

        Filters = new ObservableCollection<FilterChipItem>
        {
            new FilterChipItem { Label = "All",      IsSelected = true },
            new FilterChipItem { Label = "Strength" },
            new FilterChipItem { Label = "Cardio" },
            new FilterChipItem { Label = "Recovery" }
        };
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            _currentPhase = await _cycleService.GetCurrentPhaseAsync(userId);
            CurrentPhaseName = _currentPhase.ToString();
            ActivePlan = await _workoutRepository.GetActivePlanAsync(userId);

            if (ActivePlan is not null)
            {
                WorkoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(ActivePlan.Id);
                _allWorkouts = WorkoutDays.Select(d => new WorkoutItem
                {
                    WorkoutDayId  = d.Id,
                    Tag           = d.WorkoutType == WorkoutType.Recovery
                        ? d.RecoveryType.ToString().ToUpper()
                        : d.WorkoutType.ToString().ToUpper(),
                    Title         = d.Name,
                    Duration      = $"{d.DurationMinutes} min",
                    ExerciseCount = d.WorkoutDayExercises.Count,
                    Subtitle      = $"{d.WorkoutDayExercises.Count} exercises · {d.DurationMinutes} min",
                    WorkoutType   = d.WorkoutType,
                    RecoveryType  = d.RecoveryType,
                    PhaseBackground = GetPhaseColor(_currentPhase)
                }).ToList();

                var todayDow = (int)DateTime.Today.DayOfWeek;
                _todaysWorkoutDay = WorkoutDays.FirstOrDefault(d => d.DayOfWeek == todayDow);
            }
            else
            {
                WorkoutDays = new List<WorkoutDay>();
                _allWorkouts = new List<WorkoutItem>();
                _todaysWorkoutDay = null;
            }

            await ApplyTodayStateAsync(userId);
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyTodayStateAsync(int userId)
    {
        if (_todaysWorkoutDay is null)
        {
            IsActiveDay = false;
            IsActiveRecovery = false;
            IsPassiveRecovery = true;
            TodayTitle = "Free Day";
            TodayBodyCopy = "No workout scheduled. Rest or move lightly.";
            NextActiveDayName = FindNextActiveDayName();
            RestDayTip = GetRestDayTip(_currentPhase);
            return;
        }

        if (_todaysWorkoutDay.WorkoutType != WorkoutType.Recovery)
        {
            IsActiveDay = true;
            IsActiveRecovery = false;
            IsPassiveRecovery = false;
            TodayTitle = _todaysWorkoutDay.Name;
            TodayIntensity = GetIntensityLabel(_todaysWorkoutDay.WorkoutType, _currentPhase);
            TodayBodyCopy = $"{_todaysWorkoutDay.WorkoutDayExercises.Count} exercises · {_todaysWorkoutDay.DurationMinutes} min";
            TodaysExercises = BuildExerciseItems(_todaysWorkoutDay);
            return;
        }

        // Recovery day — apply days-since override
        var latestLog = await _workoutRepository.GetLatestActiveWorkoutLogAsync(userId);
        var effectiveType = ApplyDaysSinceOverride(_todaysWorkoutDay.RecoveryType, _currentPhase, latestLog);

        if (effectiveType == RecoveryType.ActiveRecovery)
        {
            IsActiveDay = false;
            IsActiveRecovery = true;
            IsPassiveRecovery = false;
            TodayTitle = "Active Recovery";
            TodayBodyCopy = GetActiveRecoveryCopy(_currentPhase);
            TodaysExercises = BuildExerciseItems(_todaysWorkoutDay);
        }
        else
        {
            IsActiveDay = false;
            IsActiveRecovery = false;
            IsPassiveRecovery = true;
            TodayTitle = "Rest & Restore";
            TodayBodyCopy = GetPassiveRecoveryCopy(_currentPhase);
            NextActiveDayName = FindNextActiveDayName();
            RestDayTip = GetRestDayTip(_currentPhase);
        }
    }

    private static RecoveryType ApplyDaysSinceOverride(
        RecoveryType stored, CyclePhase phase, WorkoutLog? latestLog)
    {
        if (stored != RecoveryType.PassiveRecovery) return stored;
        if (phase == CyclePhase.Menstrual) return stored;
        var daysSince = latestLog is null ? 99 : (DateTime.Today - latestLog.Date.Date).Days;
        return daysSince >= 2 ? RecoveryType.ActiveRecovery : stored;
    }

    private string FindNextActiveDayName()
    {
        if (!WorkoutDays.Any()) return string.Empty;
        var todayDow = (int)DateTime.Today.DayOfWeek;
        var next = WorkoutDays
            .Where(d => d.WorkoutType != WorkoutType.Recovery && d.DayOfWeek != todayDow)
            .OrderBy(d => (d.DayOfWeek - todayDow + 7) % 7)
            .FirstOrDefault();
        return next is null ? string.Empty : ((DayOfWeek)next.DayOfWeek).ToString();
    }

    private static ObservableCollection<ExerciseDetailItem> BuildExerciseItems(WorkoutDay day) =>
        new(day.WorkoutDayExercises.Select(we => new ExerciseDetailItem
        {
            Name       = we.Exercise?.Name ?? string.Empty,
            SetRepText = we.DurationSeconds.HasValue
                ? $"{we.DurationSeconds}s"
                : $"{we.Sets} × {we.Reps}",
            MuscleGroup = we.Exercise?.PrimaryMuscle.ToString() ?? string.Empty
        }));

    private void ShowSwap()
    {
        if (_todaysWorkoutDay is null) return;
        AlternativeWorkouts = new ObservableCollection<WorkoutItem>(
            _allWorkouts.Where(w =>
                w.WorkoutType == _todaysWorkoutDay.WorkoutType &&
                w.WorkoutDayId != _todaysWorkoutDay.Id));
        IsSwapSheetVisible = true;
    }

    private void SelectAlternative(WorkoutItem? item)
    {
        if (item is null) return;
        _todaysWorkoutDay = WorkoutDays.FirstOrDefault(d => d.Id == item.WorkoutDayId);
        if (_todaysWorkoutDay is not null)
        {
            IsActiveDay = _todaysWorkoutDay.WorkoutType != WorkoutType.Recovery;
            IsActiveRecovery = !IsActiveDay && _todaysWorkoutDay.RecoveryType == RecoveryType.ActiveRecovery;
            IsPassiveRecovery = !IsActiveDay && !IsActiveRecovery;
            TodayTitle = _todaysWorkoutDay.Name;
            TodayBodyCopy = $"{_todaysWorkoutDay.WorkoutDayExercises.Count} exercises · {_todaysWorkoutDay.DurationMinutes} min";
            TodayIntensity = GetIntensityLabel(_todaysWorkoutDay.WorkoutType, _currentPhase);
            TodaysExercises = BuildExerciseItems(_todaysWorkoutDay);
        }
        IsSwapSheetVisible = false;
    }

    private void SelectFilter(FilterChipItem? item)
    {
        if (item is null) return;
        foreach (var f in Filters)
            f.IsSelected = false;
        item.IsSelected = true;
        OnPropertyChanged(nameof(Filters));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var selected = Filters.FirstOrDefault(f => f.IsSelected);
        if (selected is null || selected.Label == "All")
        {
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts);
            return;
        }
        if (Enum.TryParse<WorkoutType>(selected.Label, out var typeFilter))
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts.Where(w => w.WorkoutType == typeFilter));
        else
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts);
    }

    private static string GetIntensityLabel(WorkoutType type, CyclePhase phase) => type switch
    {
        WorkoutType.Recovery => "GENTLE",
        WorkoutType.Cardio   => phase switch
        {
            CyclePhase.Ovulatory  => "HIGH",
            CyclePhase.Follicular => "MODERATE",
            _                     => "LOW"
        },
        _ => phase switch
        {
            CyclePhase.Ovulatory  => "PEAK",
            CyclePhase.Follicular => "HIGH",
            CyclePhase.Luteal     => "MODERATE",
            CyclePhase.Menstrual  => "LOW",
            _                     => "MODERATE"
        }
    };

    private static string GetActiveRecoveryCopy(CyclePhase phase) => phase switch
    {
        CyclePhase.Follicular => "Light movement to match your rising energy.",
        CyclePhase.Ovulatory  => "Keep momentum with gentle mobility work.",
        CyclePhase.Luteal     => "Stay moving with low-intensity stretching.",
        _                     => "Light movement to keep energy flowing."
    };

    private static string GetPassiveRecoveryCopy(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => "Your body is resetting. Full rest today.",
        CyclePhase.Luteal    => "Wind down gently. Full rest supports recovery.",
        _                    => "Full rest today."
    };

    private static string GetRestDayTip(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual  => "Warmth, hydration, and iron-rich foods support you today.",
        CyclePhase.Luteal     => "Magnesium-rich foods can ease PMS symptoms.",
        CyclePhase.Follicular => "Sleep well — recovery accelerates your follicular gains.",
        CyclePhase.Ovulatory  => "Rest builds the base for tomorrow's peak performance.",
        _                     => "Stay hydrated and prioritize sleep."
    };

    private static Color GetPhaseColor(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual  => Color.FromArgb("#FFE4EC"),
        CyclePhase.Follicular => Color.FromArgb("#E8F5E9"),
        CyclePhase.Ovulatory  => Color.FromArgb("#FFFDE7"),
        CyclePhase.Luteal     => Color.FromArgb("#EDE7F6"),
        _                     => Color.FromArgb("#F5F5F5")
    };
}
