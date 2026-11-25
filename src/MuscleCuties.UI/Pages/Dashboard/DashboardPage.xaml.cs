using System.ComponentModel;
using MuscleCuties.UI.Controls.Dashboard;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Dashboard;

/// <summary>
///     Today tab: readiness, targets, today's workout, nutrition, and the daily check-in sheet.
/// </summary>
public partial class DashboardPage : ContentPage
{
    private readonly TaskCompletionSource _initialLoadCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly InjuryLogViewModel _injuryLogVm;
    private readonly ThemeChangeObserver _themeObserver;
    private readonly DashboardViewModel _viewModel;
    private Task? _deferredCardsLoad;
    private Task? _checkInModalLoadTask;
    private Task? _injuryModalLoadTask;

    /// <summary>Takes its view model and the injury log it shares, watches theme changes, and binds before the page</summary>
    /// is presented.</summary>
    public DashboardPage(DashboardViewModel vm)
    {
        _viewModel = vm;
        _injuryLogVm = vm.InjuryLogVm;
        _themeObserver = this.CreateThemeObserver(_viewModel.RefreshThemeColors);
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Completes when the dashboard's first load finishes.</summary>
    internal Task InitialLoadCompleted => _initialLoadCompleted.Task;

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _themeObserver.Attach();
        Trace.WriteLine("[Performance][DashboardPage] Page appeared; page loading queued.");
        this.BeginPageLoad(async () =>
        {
            try
            {
                await _viewModel.LoadDataCommand.ExecuteAsync(null);
                _viewModel.RefreshThemeColors(PageLoadExtensions.IsDarkTheme());
            }
            finally
            {
                _initialLoadCompleted.TrySetResult();
            }
        });
        this.BeginDeferredLoad(LoadDeferredCardsAsync);
    }

    /// <summary>Refreshes the dashboard when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.ReattachPropertyChanged(OnViewModelPropertyChanged);
    }

    /// <summary>Starts the deferred card load once; a later call joins the running task instead of repeating it.</summary>
    private Task LoadDeferredCardsAsync()
    {
        return _deferredCardsLoad ??= LoadDeferredCardsCoreAsync();
    }

    /// <summary>Loads the cards below the fold one by one, yielding between them to keep scrolling responsive.</summary>
    private async Task LoadDeferredCardsCoreAsync()
    {
        // Keep the deferred creation order aligned with the visual reading order.
        await WorkoutCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await ReadinessLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await NutritionLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await PhaseCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await TargetsLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await TrendCardLazy.LoadIfNeededAsync(true);
    }

    /// <summary>Applies the dashboard changes the view model reports.</summary>
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var isInjuryModal = e.PropertyName == nameof(DashboardViewModel.IsInjuryModalVisible);
        if (!isInjuryModal && e.PropertyName != nameof(DashboardViewModel.IsCheckInModalVisible))
            return;

        try
        {
            if (isInjuryModal)
                await MainThread.InvokeOnMainThreadAsync(UpdateInjuryModalAsync);
            else
                await MainThread.InvokeOnMainThreadAsync(UpdateCheckInModalAsync);
        }
        catch
        {
            Trace.WriteLine(isInjuryModal
                ? "[DashboardPage] Could not present injury modal."
                : "[DashboardPage] Could not present check-in modal.");
            if (isInjuryModal)
                _viewModel.CloseInjuryModalCommand.Execute(null);
            else
                _viewModel.CloseCheckInModalCommand.Execute(null);
        }
    }

    /// <summary>Hands the injury modal to the shared helper with this page's lazy view and load task, so it is loaded</summary>
    /// once and closed through the view model.</summary>
    private async Task UpdateInjuryModalAsync()
    {
        await PageLoadExtensions.UpdateInjuryModalAsync(
            InjuryModalLazy,
            () => _injuryModalLoadTask,
            task => _injuryModalLoadTask = task,
            _viewModel.IsInjuryModalVisible,
            _injuryLogVm,
            _viewModel.CloseInjuryModalCommand);
    }

    /// <summary>Loads and shows the check-in modal only once it is needed or already loaded, so the dashboard does not</summary>
    /// pay for it on first paint.</summary>
    private async Task UpdateCheckInModalAsync()
    {
        if (!_viewModel.IsCheckInModalVisible && !CheckInModalLazy.HasLazyViewLoaded)
            return;

        if (_viewModel.IsCheckInModalVisible)
            try
            {
                await (_checkInModalLoadTask ??= CheckInModalLazy.LoadIfNeededAsync(true).AsTask());
            }
            catch
            {
                _checkInModalLoadTask = null;
                throw;
            }

        if (!CheckInModalLazy.HasLazyViewLoaded)
            return;

        var modal = (DailyCheckInModal)CheckInModalLazy.Content;
        var checkInVm = _viewModel.CheckInVm;
        if (modal.BindingContext != checkInVm)
            modal.BindingContext = checkInVm;
        modal.CloseCommand = _viewModel.CloseCheckInModalCommand;
        modal.IsOpen = _viewModel.IsCheckInModalVisible;
    }

    /// <summary>Stops the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseInjuryModalCommand.Execute(null);
        _viewModel.CloseCheckInModalCommand.Execute(null);
        _themeObserver.Detach();
        base.OnDisappearing();
    }

    /// <summary>Releases the page when navigated away from.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (PageLoadExtensions.TryCloseModalOnBack(_viewModel.IsInjuryModalVisible, _viewModel.CloseInjuryModalCommand))
            return true;

        if (PageLoadExtensions.TryCloseModalOnBack(_viewModel.IsCheckInModalVisible, _viewModel.CloseCheckInModalCommand))
            return true;

        return base.OnBackButtonPressed();
    }
}
