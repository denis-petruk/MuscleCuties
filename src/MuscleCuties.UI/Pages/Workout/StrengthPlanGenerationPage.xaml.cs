using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

public partial class StrengthPlanGenerationPage : ContentPage
{
    private const double PreviewSheetHeightFraction = 0.9;
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
            PreviewSheet.MaximumHeightRequest = Height * PreviewSheetHeightFraction;
    }
}
