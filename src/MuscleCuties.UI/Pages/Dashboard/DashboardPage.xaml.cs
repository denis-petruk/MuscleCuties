using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.UI.Controls.Dashboard;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Dashboard;

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

    public DashboardPage(DashboardViewModel vm)
    {
        _viewModel = vm;
        _injuryLogVm = vm.InjuryLogVm;
        _themeObserver = this.CreateThemeObserver(_viewModel.RefreshThemeColors);
        this.InitializeAndBind(InitializeComponent, vm);
    }

    internal Task InitialLoadCompleted => _initialLoadCompleted.Task;

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

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.ReattachPropertyChanged(OnViewModelPropertyChanged);
    }

    private Task LoadDeferredCardsAsync()
    {
        return _deferredCardsLoad ??= LoadDeferredCardsCoreAsync();
    }

    private async Task LoadDeferredCardsCoreAsync()
    {
        // Keep the deferred creation order aligned with the visual reading order.
        await PhaseCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await WorkoutCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await ReadinessLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await NutritionLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await TargetsLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await TrendCardLazy.LoadIfNeededAsync(true);
    }

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

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseInjuryModalCommand.Execute(null);
        _viewModel.CloseCheckInModalCommand.Execute(null);
        _themeObserver.Detach();
        base.OnDisappearing();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    protected override bool OnBackButtonPressed()
    {
        if (PageLoadExtensions.TryCloseModalOnBack(_viewModel.IsInjuryModalVisible, _viewModel.CloseInjuryModalCommand))
            return true;

        if (PageLoadExtensions.TryCloseModalOnBack(_viewModel.IsCheckInModalVisible, _viewModel.CloseCheckInModalCommand))
            return true;

        return base.OnBackButtonPressed();
    }
}
