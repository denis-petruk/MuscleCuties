using System.ComponentModel;
using System.Diagnostics;
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
        await PageLoadExtensions.UpdateInjuryModalAsync(
            InjuryModalLazy,
            () => _injuryModalLoadTask,
            task => _injuryModalLoadTask = task,
            _viewModel.IsInjuryModalVisible,
            _injuryLogVm,
            _viewModel.CloseInjuryModalCommand);
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseInjuryModalCommand.Execute(null);
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

        return base.OnBackButtonPressed();
    }
}
