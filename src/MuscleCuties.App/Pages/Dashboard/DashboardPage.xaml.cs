using System.Diagnostics;
using MuscleCuties.Core.ViewModels.Dashboard;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private Task? _deferredCardsLoad;
    private bool _isThemeHandlerAttached;

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
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            _viewModel.RefreshThemeColors(IsDarkTheme());
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
        await Task.Yield();
        await WorkoutCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await ReadinessLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await NutritionLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await TargetsLazy.LoadIfNeededAsync(true);
    }

    protected override void OnDisappearing()
    {
        DetachThemeHandler();
        base.OnDisappearing();
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
