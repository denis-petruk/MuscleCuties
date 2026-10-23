using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Workout;
using WorkoutViewModel = MuscleCuties.UI.ViewModels.Workout.WorkoutViewModel;

namespace MuscleCuties.UI.Pages.Workout;

public partial class WorkoutSessionPage : ContentPage
{
    private const uint CompletionGrowMilliseconds = 340;
    private const uint CompletionSettleMilliseconds = 220;
    private static readonly TimeSpan CompletionDisplayTime = TimeSpan.FromMilliseconds(1200);
    private readonly INavigationContextService _navigationContext;
    private readonly WorkoutSessionViewModel _viewModel;
    private int? _loadedWorkoutDayId;
    private int _workoutDayId;
    private int _completionRevision;

    public WorkoutSessionPage(
        WorkoutSessionViewModel vm,
        INavigationContextService navigationContext)
    {
        this.InitializeWithTiming(InitializeComponent);
        _navigationContext = navigationContext;
        BindingContext = _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_navigationContext.TryTake<int>("workoutDayId", out var workoutDayId))
            _workoutDayId = workoutDayId;

        if (_loadedWorkoutDayId == _workoutDayId)
            return;

        _loadedWorkoutDayId = _workoutDayId;
        this.BeginPageLoad(() => _viewModel.LoadSession(_workoutDayId));
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        ++_completionRevision;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Handler?.MauiContext?.Services.GetService<WorkoutViewModel>()?.Invalidate();
        base.OnNavigatingFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkoutSessionViewModel.CurrentScreen) &&
            _viewModel.IsActivitySummary && _viewModel.IsActivityComplete)
        {
            var revision = ++_completionRevision;
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    CompletionBadge.Scale = 0.75;
                    await CompletionBadge.ScaleToAsync(1.12, CompletionGrowMilliseconds, Easing.CubicOut);
                    await CompletionBadge.ScaleToAsync(1, CompletionSettleMilliseconds, Easing.CubicInOut);
                    await Task.Delay(CompletionDisplayTime);
                    if (revision == _completionRevision && _viewModel.IsActivitySummary)
                        await _viewModel.FinishSessionCommand.ExecuteAsync(null);
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

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.BackCommand.ExecuteAsync(null);
        return true;
    }
}
