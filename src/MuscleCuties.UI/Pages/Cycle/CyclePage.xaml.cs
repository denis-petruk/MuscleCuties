using System.ComponentModel;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Pages.Cycle;

public partial class CyclePage : ContentPage
{
    private readonly ThemeChangeObserver _themeObserver;
    private readonly CycleViewModel _viewModel;
    private readonly object _modalUpdateLock = new();
    private Task _modalUpdate = Task.CompletedTask;

    public CyclePage(CycleViewModel vm)
    {
        _viewModel = vm;
        _themeObserver = this.CreateThemeObserver(_viewModel.RefreshThemeColors);
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _themeObserver.Attach();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.ReattachPropertyChanged(OnViewModelPropertyChanged);
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            _viewModel.RefreshThemeColors(PageLoadExtensions.IsDarkTheme());
        });
        this.BeginDeferredLoad(LoadDeferredContentAsync);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(CycleViewModel.IsDatePhaseModalVisible) or
            nameof(CycleViewModel.IsCycleWarningPopupVisible) or
            nameof(CycleViewModel.IsQuickLogSheetVisible) or
            nameof(CycleViewModel.IsPhaseSummarySheetVisible)))
            return;

        lock (_modalUpdateLock)
            _modalUpdate = PresentModalsAfterAsync(_modalUpdate);
    }

    private async Task PresentModalsAfterAsync(Task previousUpdate)
    {
        try
        {
            await previousUpdate;
        }
        catch
        {
            // A failed update cannot prevent a later visibility change from retrying.
        }

        await PageLoadExtensions.PresentModalSafelyAsync(_viewModel, nameof(CyclePage), async () =>
        {
            await DatePhaseModal.LoadIfNeededAsync(_viewModel.IsDatePhaseModalVisible);
            await WarningPopup.LoadIfNeededAsync(_viewModel.IsCycleWarningPopupVisible);
            await QuickLogSheet.LoadIfNeededAsync(_viewModel.IsQuickLogSheetVisible);
            await PhaseSummarySheet.LoadIfNeededAsync(_viewModel.IsPhaseSummarySheetVisible);
        });
    }

    private async Task LoadDeferredContentAsync()
    {
        await HeroCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await CalendarCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await PhaseExplorerLazy.LoadIfNeededAsync(true);
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsQuickLogSheetVisible)
        {
            _viewModel.CloseQuickLogSheetCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsPhaseSummarySheetVisible)
        {
            _viewModel.ClosePhaseSummarySheetCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsDatePhaseModalVisible)
        {
            _viewModel.CloseDatePhaseModalCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsCycleWarningPopupVisible)
        {
            _viewModel.CloseCycleWarningPopupCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _themeObserver.Detach();
        base.OnDisappearing();
    }

    internal void PrepareTheme()
    {
        _viewModel.RefreshThemeColors(PageLoadExtensions.IsDarkTheme());
    }
}
