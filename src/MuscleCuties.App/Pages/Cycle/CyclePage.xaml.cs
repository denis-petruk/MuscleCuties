using System.ComponentModel;
using System.Diagnostics;
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
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            _viewModel.RefreshThemeColors(IsDarkTheme());
        });
        this.BeginDeferredLoad(LoadDeferredContentAsync);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(CycleViewModel.IsDatePhaseModalVisible) or
            nameof(CycleViewModel.IsCycleWarningPopupVisible)))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DatePhaseModal.LoadIfNeededAsync(_viewModel.IsDatePhaseModalVisible);
                await WarningPopup.LoadIfNeededAsync(_viewModel.IsCycleWarningPopupVisible);
            });
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[CyclePage] Could not present modal: {exception}");
            _viewModel.IsLoadError = true;
        }
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
