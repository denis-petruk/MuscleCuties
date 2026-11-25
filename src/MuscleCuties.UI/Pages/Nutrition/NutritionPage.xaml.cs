using System.ComponentModel;
using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.ViewModels.Nutrition;

namespace MuscleCuties.UI.Pages.Nutrition;

/// <summary>
///     Nutrition tab: daily meals, macro balance, phase focus, and the add-meal and suggestion sheets.
/// </summary>
public partial class NutritionPage : ContentPage
{
    private readonly NutritionViewModel _viewModel;
    private int _lastCelebrationToken;

    /// <summary>Takes its view model and binds before presentation so the loading overlay is active immediately.</summary>
    public NutritionPage(NutritionViewModel vm)
    {
        _viewModel = vm;
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Reloads the day when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.ReattachPropertyChanged(OnViewModelPropertyChanged);
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            await LoadRequestedViewsAsync();
        });
        this.BeginDeferredLoad(LoadDeferredCardsAsync);
    }

    /// <summary>Opens the diary, and on failure reports it on the page instead of leaving the tap doing nothing.</summary>
    private async void OnOpenDailyFoodLogClicked(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(NutritionNavigation.FoodLogRoute);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"[Nutrition] Diary navigation failed: {exception.GetType().Name}");
            _viewModel.ReportDiaryNavigationFailure();
        }
    }

    /// <summary>Loads the cards below the fold in sequence, yielding between them.</summary>
    private async Task LoadDeferredCardsAsync()
    {
        await PhaseFocusLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await BalanceCardLazy.LoadIfNeededAsync(true);
    }

    /// <summary>Releases the page when navigated away from.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    /// <summary>Stops the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDisappearing();
    }

    /// <summary>Applies the nutrition changes the view model reports.</summary>
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(NutritionViewModel.IsMealEditorVisible) or
            nameof(NutritionViewModel.IsMealEntryModalVisible) or
            nameof(NutritionViewModel.IsBreakdownModalVisible) or
            nameof(NutritionViewModel.IsSuggestionModalVisible) or
            nameof(NutritionViewModel.IsMealDetailVisible) or
            nameof(NutritionViewModel.CelebrationToken)))
            return;

        await PageLoadExtensions.PresentModalSafelyAsync(_viewModel, nameof(NutritionPage), async () =>
        {
            await LoadRequestedViewsAsync();
            if (e.PropertyName != nameof(NutritionViewModel.CelebrationToken) ||
                _viewModel.CelebrationToken <= 0 || _viewModel.CelebrationToken == _lastCelebrationToken)
                return;

            _lastCelebrationToken = _viewModel.CelebrationToken;
            SaluteOverlay.IsVisible = true;
            try
            {
                await SaluteOverlay.LoadIfNeededAsync(true);
                await ((PhaseSaluteOverlay)SaluteOverlay.Content).PlayAsync(_viewModel.CelebrationIconSource);
            }
            finally
            {
                SaluteOverlay.IsVisible = false;
            }
        });
    }

    /// <summary>Loads whichever modal the view model has asked for, treating an editor-to-entry handoff as one</summary>
    /// presentation rather than two.</summary>
    private async Task LoadRequestedViewsAsync()
    {
        var handoffToEditor = NutritionMealEntryHandoff.IsHandoff(
            _viewModel.IsMealEditorVisible, _viewModel.IsMealEntryModalVisible);
        var editorPresented = false;
        await MealEditor.LoadIfNeededAsync(_viewModel.IsMealEditorVisible);
        if (handoffToEditor && MealEditor.Content is AnimatedModalView editorModal)
        {
            // The editor sits behind the chooser in the page grid. Finish its presentation
            // before dismissing the chooser so no frame exposes the page between the two sheets.
            editorPresented = editorModal.CompleteOpenTransition();
        }

        await LoadBreakdownViewAsync();
        await Suggestions.LoadIfNeededAsync(_viewModel.IsSuggestionModalVisible);
        await MealDetail.LoadIfNeededAsync(_viewModel.IsMealDetailVisible);
        await LoadMealEntryViewAsync();

        if (NutritionMealEntryHandoff.CanCloseChooser(
                _viewModel.IsMealEditorVisible,
                _viewModel.IsMealEntryModalVisible,
                editorPresented))
        {
            // Let the tap that chose the meal finish before the chooser disappears; otherwise the
            // touch can fall through to the editor that is now occupying the same screen.
            await Task.Yield();
            if (NutritionMealEntryHandoff.CanCloseChooser(
                    _viewModel.IsMealEditorVisible,
                    _viewModel.IsMealEntryModalVisible,
                    editorPresented))
            {
                _viewModel.CloseMealEntryCommand.Execute(null);
            }
        }
    }

    /// <summary>Loads the breakdown panel only when it is visible or has been loaded before.</summary>
    private async Task LoadBreakdownViewAsync()
    {
        if (!_viewModel.IsBreakdownModalVisible && !Breakdown.HasLazyViewLoaded)
            return;

        await Breakdown.LoadIfNeededAsync(_viewModel.IsBreakdownModalVisible);
        if (Breakdown.HasLazyViewLoaded && Breakdown.Content is BindableObject breakdownModal &&
            !ReferenceEquals(breakdownModal.BindingContext, _viewModel.Breakdown))
        {
            breakdownModal.BindingContext = _viewModel.Breakdown;
        }
    }

    /// <summary>
    /// NM01: Add meal used to request a full-page sheet host that never initialized, leaving an
    /// invisible layer intercepting input and making Nutrition unusable. The host is loaded before it
    /// can be shown, and a sheet that fails to load is released back to an interactive page.
    /// </summary>
    private async Task LoadMealEntryViewAsync()
    {
        if (!_viewModel.IsMealEntryModalVisible)
            return;

        try
        {
            await MealEntry.LoadIfNeededAsync(true);
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[NutritionPage] Meal entry sheet could not load ({exception.GetType().Name}).");
            _viewModel.CloseMealEntryCommand.Execute(null);
        }
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsMealDetailVisible)
        {
            _viewModel.CloseMealDetailCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsSuggestionModalVisible)
        {
            _viewModel.CloseSuggestionModalCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsBreakdownModalVisible)
        {
            _viewModel.CloseMicronutrientsModalCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsMealEntryModalVisible)
        {
            _viewModel.CloseMealEntryCommand.Execute(null);
            return true;
        }

        if (!_viewModel.IsMealEditorVisible)
            return base.OnBackButtonPressed();

        if (_viewModel.IsFoodFinderVisible)
            _viewModel.MealEditorBackCommand.Execute(null);
        else
            _viewModel.ToggleMealEditorCommand.Execute(null);
        return true;
    }
}
