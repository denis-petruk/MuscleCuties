using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Workout;
using WorkoutViewModel = MuscleCuties.UI.ViewModels.Workout.WorkoutViewModel;

namespace MuscleCuties.UI.Pages.Workout;

public partial class WorkoutSessionPage : ContentPage
{
    private readonly INavigationContextService _navigationContext;
    private readonly WorkoutSessionViewModel _viewModel;
    private readonly WorkoutViewModel _workoutViewModel;
    private int? _loadedWorkoutDayId;
    private int _workoutDayId;
    private string? _activityTag;

    public WorkoutSessionPage(
        WorkoutSessionViewModel vm,
        INavigationContextService navigationContext,
        WorkoutViewModel workoutViewModel)
    {
        this.InitializeWithTiming(InitializeComponent);
        _navigationContext = navigationContext;
        _workoutViewModel = workoutViewModel;
        BindingContext = _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_navigationContext.TryTake<int>("workoutDayId", out var workoutDayId))
            _workoutDayId = workoutDayId;
        _navigationContext.TryTake<string>("workoutActivityTag", out _activityTag);

        if (_loadedWorkoutDayId == _workoutDayId)
            return;

        _loadedWorkoutDayId = _workoutDayId;
        var activityTag = _activityTag;
        _activityTag = null;
        this.BeginPageLoad(() => _viewModel.LoadSession(_workoutDayId, activityTag));
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _workoutViewModel.Invalidate();
        base.OnNavigatingFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkoutSessionViewModel.CurrentScreen) &&
            _viewModel.IsActivitySummary && _viewModel.IsActivityComplete)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    CompletionBadge.Scale = AppThemeResources.GetDouble("UiScaleCompletionStart");
                    await CompletionBadge.ScaleToAsync(
                        AppThemeResources.GetDouble("UiScaleCelebrationPeak"),
                        (uint)AppThemeResources.GetDouble("UiMotionDuration340"), Easing.CubicOut);
                    await CompletionBadge.ScaleToAsync(
                        AppThemeResources.GetDouble("UiScaleFull"),
                        (uint)AppThemeResources.GetDouble("UiMotionDuration220"), Easing.CubicInOut);
                });
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"[WorkoutSessionPage] Completion transition failed ({exception.GetType().Name}).");
            }
            return;
        }
        if (e.PropertyName != nameof(WorkoutSessionViewModel.IsSwapPanelVisible))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await SwapPanelLazy.LoadIfNeededAsync(_viewModel.IsSwapPanelVisible);
            });
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[WorkoutSessionPage] Could not present swap panel ({exception.GetType().Name}).");
        }
    }

    private void OnEffortDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider { BindingContext: WorkoutExerciseItem exercise } slider)
            exercise.CommitEffortSliderValue(slider.Value);
    }

    private async void OnOptionalExerciseToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Switch { BindingContext: WorkoutExerciseItem exercise }) return;
        try { await _viewModel.SaveOptionalExerciseAsync(exercise); }
        catch (Exception exception)
        {
            Trace.WriteLine($"[WorkoutSessionPage] Could not save optional exercise marker ({exception.GetType().Name}).");
        }
    }

    private async void OnOptionalSetsChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is not Stepper { BindingContext: WorkoutExerciseItem exercise }) return;
        try { await _viewModel.SaveOptionalExerciseAsync(exercise); }
        catch (Exception exception)
        {
            Trace.WriteLine($"[WorkoutSessionPage] Could not save optional set count ({exception.GetType().Name}).");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.BackCommand.ExecuteAsync(null);
        return true;
    }
}
