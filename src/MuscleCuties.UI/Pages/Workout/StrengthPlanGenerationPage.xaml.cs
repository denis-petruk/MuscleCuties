using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Pages.Workout;

public partial class StrengthPlanGenerationPage : ContentPage
{
    private readonly StrengthPlanGenerationViewModel _viewModel;

    // CR01: a deferred load must not start after this page has left the navigation stack. The
    // appearance version is bumped on entry and on departure, so a queued load that loses the race is
    // dropped instead of mutating a popped page while the native layout is being torn down.
    private long _appearanceVersion;

    public StrengthPlanGenerationPage(StrengthPlanGenerationViewModel viewModel)
    {
        this.InitializeAndBind(InitializeComponent, viewModel);
        _viewModel = viewModel;
    }

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

    protected override void OnDisappearing()
    {
        _appearanceVersion++;
        _viewModel.NotifyAppearanceExited();
        base.OnDisappearing();
    }

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

    private void OnPageSizeChanged(object? sender, EventArgs args)
    {
        if (Height > 0)
            PreviewSheet.MaximumHeightRequest = Math.Min(BottomSheetSizing.MaximumSheetHeight,
                Height * AppThemeResources.GetDouble("UiPreviewSheetHeightFraction"));
    }
}
