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

    [ObservableProperty] private WorkoutPlan? _activePlan;
    [ObservableProperty] private List<WorkoutDay> _workoutDays = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    public AsyncRelayCommand LoadDataCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public RelayCommand<WorkoutItem> OpenDetailCommand { get; }

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
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);
            CurrentPhaseName = phase.ToString();
            ActivePlan = await _workoutRepository.GetActivePlanAsync(userId);

            if (ActivePlan is not null)
            {
                WorkoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(ActivePlan.Id);
                _allWorkouts = WorkoutDays.Select(d => new WorkoutItem
                {
                    WorkoutDayId  = d.Id,
                    Tag           = d.WorkoutType.ToString().ToUpper(),
                    Title         = d.Name,
                    Duration      = $"{d.DurationMinutes} min",
                    ExerciseCount = d.WorkoutDayExercises.Count,
                    Subtitle      = $"{d.WorkoutDayExercises.Count} exercises · {d.DurationMinutes} min",
                    WorkoutType   = d.WorkoutType,
                    PhaseBackground = GetPhaseColor(phase)
                }).ToList();
            }
            else
            {
                WorkoutDays = new List<WorkoutDay>();
                _allWorkouts = new List<WorkoutItem>();
            }

            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
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

    private static Color GetPhaseColor(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual  => Color.FromArgb("#FFE4EC"),
        CyclePhase.Follicular => Color.FromArgb("#E8F5E9"),
        CyclePhase.Ovulatory  => Color.FromArgb("#FFFDE7"),
        CyclePhase.Luteal     => Color.FromArgb("#EDE7F6"),
        _                     => Color.FromArgb("#F5F5F5")
    };
}
