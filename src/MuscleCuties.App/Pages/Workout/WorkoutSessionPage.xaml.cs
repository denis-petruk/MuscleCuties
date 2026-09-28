using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.App.Services.Navigation;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutSessionPage : ContentPage
{
    private readonly INavigationContextService _navigationContext;
    private readonly WorkoutSessionViewModel _viewModel;
    private int _workoutDayId;
    private int? _loadedWorkoutDayId;

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
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Handler?.MauiContext?.Services.GetService<WorkoutViewModel>()?.Invalidate();
        base.OnNavigatingFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.BackCommand.ExecuteAsync(null);
        return true;
    }
}
