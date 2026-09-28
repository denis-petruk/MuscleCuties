using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.App.Controls.Injury;
using MuscleCuties.Core.ViewModels.Dashboard;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private Task? _deferredCardsLoad;
    private Task? _injuryModalLoadTask;
    private readonly TaskCompletionSource _initialLoadCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _isThemeHandlerAttached;

    internal Task InitialLoadCompleted => _initialLoadCompleted.Task;

    public DashboardPage(DashboardViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        this.BindWithTiming(vm, started);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachThemeHandler();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        this.BeginPageLoad(async () =>
        {
            try
            {
                await _viewModel.LoadDataCommand.ExecuteAsync(null);
                _viewModel.RefreshThemeColors(IsDarkTheme());
            }
            finally
            {
                _initialLoadCompleted.TrySetResult();
            }
        });
    }

    private void OnDashboardLoaded(object? sender, EventArgs e)
    {
        this.BeginDeferredLoad(LoadDeferredCardsAsync);
    }

    private Task LoadDeferredCardsAsync()
    {
        return _deferredCardsLoad ??= LoadDeferredCardsCoreAsync();
    }

    private async Task LoadDeferredCardsCoreAsync()
    {
        await PhaseCardLazy.LoadIfNeededAsync(true);
        await Task.Delay(33);
        await CalendarCardLazy.LoadIfNeededAsync(true);
        await Task.Delay(33);
        await WorkoutCardLazy.LoadIfNeededAsync(true);
        await Task.Delay(33);
        await ReadinessLazy.LoadIfNeededAsync(true);
        await Task.Delay(33);
        await NutritionLazy.LoadIfNeededAsync(true);
        await Task.Delay(33);
        await TargetsLazy.LoadIfNeededAsync(true);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DashboardViewModel.IsInjuryModalVisible))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(UpdateInjuryModalAsync);
        }
        catch
        {
            Trace.WriteLine("[DashboardPage] Could not present injury modal.");
            _viewModel.IsLoadError = true;
        }
    }

    private async Task UpdateInjuryModalAsync()
    {
        if (!_viewModel.IsInjuryModalVisible && !InjuryModalLazy.HasLazyViewLoaded)
            return;

        if (_viewModel.IsInjuryModalVisible)
        {
            try
            {
                await (_injuryModalLoadTask ??= InjuryModalLazy.LoadIfNeededAsync(true).AsTask());
            }
            catch
            {
                _injuryModalLoadTask = null;
                throw;
            }
        }

        if (!InjuryModalLazy.HasLazyViewLoaded)
            return;

        var modal = (InjuryLogModal)InjuryModalLazy.Content;
        if (modal.BindingContext != _viewModel.InjuryLogVm)
            modal.BindingContext = _viewModel.InjuryLogVm;
        modal.CloseCommand = _viewModel.CloseInjuryModalCommand;
        modal.IsOpen = _viewModel.IsInjuryModalVisible;
    }

    protected override void OnDisappearing()
    {
        _viewModel.CloseInjuryModalCommand.Execute(null);
        DetachThemeHandler();
        base.OnDisappearing();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsInjuryModalVisible)
            return base.OnBackButtonPressed();

        _viewModel.CloseInjuryModalCommand.Execute(null);
        return true;
    }

    private void AttachThemeHandler()
    {
        if (_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeHandlerAttached = true;
    }

    private void DetachThemeHandler()
    {
        if (!_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        _isThemeHandlerAttached = false;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _viewModel.RefreshThemeColors(e.RequestedTheme == AppTheme.Dark);
    }

    private static bool IsDarkTheme()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }
}
