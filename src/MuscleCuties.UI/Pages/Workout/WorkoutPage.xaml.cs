using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.UI.Controls.Injury;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _viewModel;
    private readonly WorkoutSessionViewModel _sessionViewModel;
    private readonly INavigationContextService _navigationContext;
    private Task? _injuryModalLoadTask;

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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // A saved preference can invalidate the retained tab while it is off screen.
        // Loading on appearance makes the regenerated plan visible on return.
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.CloseInjuryModalCommand.Execute(null);
        _viewModel.SessionActivitiesRequested -= OnSessionActivitiesRequested;
        SessionActivitiesOverlay.IsVisible = false;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private async Task OnSessionActivitiesRequested(int workoutDayId)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            SessionActivitiesOverlay.IsVisible = true;
            SessionActivitiesOverlay.BindingContext = _sessionViewModel;
            SessionActivitiesOverlay.IsEnabled = false;
        });

        await _sessionViewModel.LoadSession(workoutDayId);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SessionActivitiesOverlay.IsEnabled = true;
            SessionActivitiesOverlay.IsVisible = true;
        });
    }

    private void CloseSessionActivities()
    {
        SessionActivitiesOverlay.IsVisible = false;
    }

    private void OnCloseSessionActivitiesClicked(object? sender, EventArgs e) => CloseSessionActivities();

    private async void OnSessionActivitySelected(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: WorkoutActivitySectionItem activity } ||
            _sessionViewModel.IsBusy || string.IsNullOrWhiteSpace(activity.Tag))
            return;

        SessionActivitiesOverlay.IsVisible = false;
        _navigationContext.Set("workoutDayId", _sessionViewModel.WorkoutDayId);
        _navigationContext.Set("workoutActivityTag", activity.Tag);
        await Shell.Current.GoToAsync(nameof(WorkoutSessionPage));
    }

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

    protected override bool OnBackButtonPressed()
    {
        if (SessionActivitiesOverlay.IsVisible)
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
