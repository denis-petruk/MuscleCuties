using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

/// <summary>
///     Progress page shown while the first strength plan is generated.
/// </summary>
public partial class StrengthPlanGenerationPage : ContentPage
{
    private readonly StrengthPlanGenerationViewModel _viewModel;

    // CR01: a deferred load must not start after this page has left the navigation stack. The
    // appearance version is bumped on entry and on departure, so a queued load that loses the race is
    // dropped instead of mutating a popped page while the native layout is being torn down.
    private long _appearanceVersion;

    /// <summary>Initializes the page and its view model.</summary>
    public StrengthPlanGenerationPage(StrengthPlanGenerationViewModel viewModel)
    {
        this.InitializeAndBind(InitializeComponent, viewModel);
        _viewModel = viewModel;
    }

    /// <summary>Starts the generation when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var appearanceVersion = ++_appearanceVersion;
        _viewModel.NotifyAppearanceEntered();

        // The appearance check runs before the load wrapper touches error/loading state, so a
        // departure cannot leave a deferred callback resetting an error on a page that has gone.
        this.BeginPageLoad(
            () => _viewModel.LoadCommand.ExecuteAsync(null),
            () => appearanceVersion == _appearanceVersion);
    }

    /// <summary>Stops the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _appearanceVersion++;
        _viewModel.NotifyAppearanceExited();
        base.OnDisappearing();
    }

    /// <summary>Cancels the generation before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsBusy)
            return true;

        if (_viewModel.HasPreview)
        {
            _viewModel.ClosePreviewCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsTrainNavigationPending)
        {
            _viewModel.OpenTrainCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>Updates the layout when the page size changes.</summary>
    private void OnPageSizeChanged(object? sender, EventArgs args)
    {
        if (Height <= 0)
            return;

        // MaximumHeightRequest alone lets the sheet measure to its full content height, which pushes
        // the action footer below the screen at accessibility sizes. A matching HeightRequest gives
        // the star-sized ScrollView the remaining space and keeps Apply plan on screen.
        var sheetHeight = Math.Min(BottomSheetSizing.MaximumSheetHeight,
            Height * AppThemeResources.GetDouble("UiPreviewSheetHeightFraction"));
        PreviewSheet.HeightRequest = sheetHeight;
        PreviewSheet.MaximumHeightRequest = sheetHeight;
    }
}
