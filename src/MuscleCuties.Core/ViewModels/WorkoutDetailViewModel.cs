using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class WorkoutDetailViewModel : ObservableObject
{
    private readonly IWorkoutRepository _repo;
    private readonly IAuthService _authService;
    private readonly int _workoutDayId;
    private readonly Action _onCompletionLogged;

    [ObservableProperty] private string _workoutTitle = string.Empty;
    [ObservableProperty] private string _workoutTypeLabel = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<ExerciseItem> _exercises = new();

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand LogCompletionCommand { get; }

    public WorkoutDetailViewModel(
        IWorkoutRepository repo,
        IAuthService authService,
        int workoutDayId,
        Action onCompletionLogged)
    {
        _repo = repo;
        _authService = authService;
        _workoutDayId = workoutDayId;
        _onCompletionLogged = onCompletionLogged;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        LogCompletionCommand = new AsyncRelayCommand(LogCompletionAsync);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var day = await _repo.GetWorkoutDayWithExercisesAsync(_workoutDayId);
            if (day is null)
            {
                Exercises = new ObservableCollection<ExerciseItem>();
                return;
            }

            WorkoutTitle = day.Name;
            WorkoutTypeLabel = day.WorkoutType.ToString().ToUpper();
            DurationText = $"{day.DurationMinutes} min";

            Exercises = new ObservableCollection<ExerciseItem>(
                day.WorkoutDayExercises
                    .Where(we => we.Exercise is not null)
                    .Select(we => new ExerciseItem
                    {
                        Name            = we.Exercise!.Name,
                        Description     = we.Exercise.Description,
                        PrimaryMuscle   = we.Exercise.PrimaryMuscle,
                        Sets            = we.Sets,
                        Reps            = we.Reps,
                        DurationSeconds = we.DurationSeconds
                    }));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LogCompletionAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        await _repo.AddWorkoutLogAsync(new WorkoutLog
        {
            UserId            = userId,
            WorkoutDayId      = _workoutDayId,
            Date              = DateTime.UtcNow.Date,
            CompletionPercent = 100,
            CreatedAt         = DateTime.UtcNow
        });
        _onCompletionLogged();
    }
}
