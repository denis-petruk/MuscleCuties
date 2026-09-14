using MuscleCuties.Core.ViewModels.Cycle;

namespace MuscleCuties.App.Pages.Cycle;

public partial class CyclePage : ContentPage
{
    private readonly CycleViewModel _viewModel;
    private bool _isThemeHandlerAttached;

    public CyclePage(CycleViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachThemeHandler();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (BindingContext is null)
            BindingContext = _viewModel;
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            _viewModel.RefreshThemeColors(IsDarkTheme());
        });
        this.BeginDeferredLoad(LoadDeferredContentAsync);
    }

    private async Task LoadDeferredContentAsync()
    {
        await PhaseGuideLazy.LoadIfNeededAsync(true);
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

    internal void PrepareTheme()
    {
        _viewModel.RefreshThemeColors(IsDarkTheme());
    }

    private static bool IsDarkTheme()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }
}
