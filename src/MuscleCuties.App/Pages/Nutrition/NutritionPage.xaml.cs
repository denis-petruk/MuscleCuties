using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.App.Controls.Shared;
using MuscleCuties.Core.ViewModels.Nutrition;

namespace MuscleCuties.App.Pages.Nutrition;

public partial class NutritionPage : ContentPage
{
    private readonly NutritionViewModel _viewModel;
    private int _lastCelebrationToken;

    public NutritionPage(NutritionViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.BindWithTiming(vm, started);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        this.BeginPageLoad(async () =>
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            await LoadRequestedViewsAsync();
        });
        this.BeginDeferredLoad(LoadDeferredCardsAsync);
    }

    private async Task LoadDeferredCardsAsync()
    {
        await PhaseFocusLazy.LoadIfNeededAsync(true);
        await Task.Yield();
        await BalanceCardLazy.LoadIfNeededAsync(true);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(NutritionViewModel.IsMealEditorVisible) or
            nameof(NutritionViewModel.IsBreakdownModalVisible) or
            nameof(NutritionViewModel.IsSuggestionModalVisible) or
            nameof(NutritionViewModel.IsMealDetailVisible) or
            nameof(NutritionViewModel.CelebrationToken)))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadRequestedViewsAsync();
                if (e.PropertyName != nameof(NutritionViewModel.CelebrationToken) ||
                    _viewModel.CelebrationToken <= 0 || _viewModel.CelebrationToken == _lastCelebrationToken)
                    return;

                _lastCelebrationToken = _viewModel.CelebrationToken;
                await SaluteOverlay.LoadIfNeededAsync(true);
                await ((PhaseSaluteOverlay)SaluteOverlay.Content).PlayAsync(_viewModel.CelebrationIconSource);
            });
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[NutritionPage] Could not present view ({exception.GetType().Name}).");
            _viewModel.IsLoadError = true;
        }
    }

    private async Task LoadRequestedViewsAsync()
    {
        await MealEditor.LoadIfNeededAsync(_viewModel.IsMealEditorVisible);
        await Breakdown.LoadIfNeededAsync(_viewModel.IsBreakdownModalVisible);
        await Suggestions.LoadIfNeededAsync(_viewModel.IsSuggestionModalVisible);
        await MealDetail.LoadIfNeededAsync(_viewModel.IsMealDetailVisible);
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsMealEditorVisible)
            return base.OnBackButtonPressed();

        if (_viewModel.IsFoodFinderVisible)
            _viewModel.MealEditorBackCommand.Execute(null);
        else
            _viewModel.ToggleMealEditorCommand.Execute(null);
        return true;
    }
}
