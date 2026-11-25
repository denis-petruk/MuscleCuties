using System.ComponentModel;
using MuscleCuties.UI.Controls.Injury;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

/// <summary>
///     Train tab: this week's plan, its filters, and the injury log.
/// </summary>
public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _viewModel;
    private readonly WorkoutSessionViewModel _sessionViewModel;
    private readonly INavigationContextService _navigationContext;
    private Task? _injuryModalLoadTask;

    /// <summary>Initializes the page with its view models and navigation context.</summary>
    public WorkoutPage(WorkoutViewModel vm, WorkoutSessionViewModel sessionViewModel,
        INavigationContextService navigationContext)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        _sessionViewModel = sessionViewModel;
        _navigationContext = navigationContext;
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.BindWithTiming(vm, started);
    }

    /// <summary>Reads the day it was opened for and loads it.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.SessionActivitiesRequested -= OnSessionActivitiesRequested;
        _viewModel.SessionActivitiesRequested += OnSessionActivitiesRequested;
        if (_viewModel.IsInjuryModalVisible)
            OnViewModelPropertyChanged(_viewModel,
                new PropertyChangedEventArgs(nameof(WorkoutViewModel.IsInjuryModalVisible)));
        this.BeginDeferredLoad(() => FeaturedWorkoutLazy.LoadIfNeededAsync(true).AsTask());
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // A saved preference can invalidate the retained tab while it is off screen.
        // Loading on appearance makes the regenerated plan visible on return.
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Releases the page when navigated away from.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.CloseInjuryModalCommand.Execute(null);
        _viewModel.SessionActivitiesRequested -= OnSessionActivitiesRequested;
        CloseSessionActivities();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    /// <summary>Opens the activity picker for a session, on the main thread because it is driven by a view-model event.</summary>
    private async Task OnSessionActivitiesRequested(int workoutDayId)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _viewModel.IsSessionActivitiesVisible = true;
            SessionActivitiesOverlay.BindingContext = _sessionViewModel;
            SessionActivitiesOverlay.IsOpen = true;
        });

        await _sessionViewModel.LoadSession(workoutDayId);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SessionActivitiesOverlay.IsOpen = true;
        });
    }

    /// <summary>Closes the picker and clears the view model's visibility flag together.</summary>
    private void CloseSessionActivities()
    {
        SessionActivitiesOverlay.IsOpen = false;
        _viewModel.IsSessionActivitiesVisible = false;
    }

    /// <summary>Closes the session activity list.</summary>
    private void OnCloseSessionActivitiesClicked(object? sender, EventArgs e) => CloseSessionActivities();

    /// <summary>Starts the session for the tapped activity, ignoring taps while busy or on a row without a tag.</summary>
    private async void OnSessionActivitySelected(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: WorkoutActivitySectionItem activity } ||
            _sessionViewModel.IsBusy || string.IsNullOrWhiteSpace(activity.Tag))
            return;

        CloseSessionActivities();
        _navigationContext.Set("workoutDayId", _sessionViewModel.WorkoutDayId);
        _navigationContext.Set("workoutActivityTag", activity.Tag);
        await Shell.Current.GoToAsync(nameof(WorkoutSessionPage));
    }

    /// <summary>Applies the workout changes the view model reports.</summary>
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkoutViewModel.IsInjuryModalVisible))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(UpdateInjuryModalAsync);
        }
        catch
        {
            Trace.WriteLine("[WorkoutPage] Could not present injury modal.");
            _viewModel.CloseInjuryModalCommand.Execute(null);
        }
    }

    /// <summary>Loads and shows the injury modal only when needed, closing it again if it cannot be presented.</summary>
    private async Task UpdateInjuryModalAsync()
    {
        if (!_viewModel.IsInjuryModalVisible && !InjuryModalLazy.HasLazyViewLoaded)
            return;

        if (_viewModel.IsInjuryModalVisible)
            try
            {
                await (_injuryModalLoadTask ??= InjuryModalLazy.LoadIfNeededAsync(true).AsTask());
            }
            catch
            {
                _injuryModalLoadTask = null;
                throw;
            }

        if (!InjuryModalLazy.HasLazyViewLoaded)
            return;

        var modal = (InjuryLogModal)InjuryModalLazy.Content;
        if (modal.BindingContext != _viewModel.InjuryLogVm)
            modal.BindingContext = _viewModel.InjuryLogVm;
        modal.CloseCommand = _viewModel.CloseInjuryModalCommand;
        modal.IsOpen = _viewModel.IsInjuryModalVisible;
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsRescheduleModalVisible)
        {
            _viewModel.CloseRescheduleModalCommand.Execute(null);
            return true;
        }

        if (SessionActivitiesOverlay.IsOpen || _viewModel.IsSessionActivitiesVisible)
        {
            CloseSessionActivities();
            return true;
        }

        if (!_viewModel.IsInjuryModalVisible)
            return base.OnBackButtonPressed();

        _viewModel.CloseInjuryModalCommand.Execute(null);
        return true;
    }
}
