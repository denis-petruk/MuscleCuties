using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Pages.Workout;

public partial class StrengthPlanGenerationPage : ContentPage
{
    private readonly StrengthPlanGenerationViewModel _viewModel;

    public StrengthPlanGenerationPage(StrengthPlanGenerationViewModel viewModel)
    {
        this.InitializeAndBind(InitializeComponent, viewModel);
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.BeginPageLoad(() => _viewModel.LoadCommand.ExecuteAsync(null));
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
