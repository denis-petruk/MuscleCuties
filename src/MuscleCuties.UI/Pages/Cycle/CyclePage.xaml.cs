using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Pages.Cycle;

/// <summary>
///     Cycle tab: today's phase, the calendar strip, quick logging, and phase detail.
/// </summary>
public partial class CyclePage : ContentPage
{
    private readonly ThemeChangeObserver _themeObserver;
    private readonly CycleViewModel _viewModel;
    private readonly object _modalUpdateLock = new();
    private Task _modalUpdate = Task.CompletedTask;

    /// <summary>Takes its view model, subscribes to theme changes for the hand-drawn phase colours, and binds before</summary>
    /// presentation so the loading overlay is live from the first frame.</summary>
    public CyclePage(CycleViewModel vm)
    {
        _viewModel = vm;
        _themeObserver = this.CreateThemeObserver(_viewModel.RefreshThemeColors);
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _themeObserver.Attach();
    }

    /// <summary>Reloads the calendar when the page is navigated to.</summary>
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

    /// <summary>Releases the page's view model when it is left.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    /// <summary>Updates the page when the selected day changes.</summary>
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

    /// <summary>Presents the modals in call order, so a quickly toggled sheet cannot overtake the presentation already</summary>
    /// in flight.</summary>
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

    /// <summary>Loads the below-the-fold cards one at a time with a yield between them, so the first paint is not</summary>
    /// blocked by work the user may not scroll to.</summary>
    private async Task LoadDeferredContentAsync()
    {
        await HeroCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await CalendarCardLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await PhaseExplorerLazy.LoadIfNeededAsync(true);
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
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

    /// <summary>Stops the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _themeObserver.Detach();
        base.OnDisappearing();
    }
}
