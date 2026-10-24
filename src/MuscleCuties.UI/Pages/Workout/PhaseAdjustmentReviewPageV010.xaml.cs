using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

public partial class PhaseAdjustmentReviewPageV010 : ContentPage
{
    private readonly PhaseAdjustmentReviewViewModelV010 _viewModel;

    public PhaseAdjustmentReviewPageV010(PhaseAdjustmentReviewViewModelV010 viewModel)
    {
        this.InitializeAndBind(InitializeComponent, viewModel);
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.BeginPageLoad(() => _viewModel.LoadCommand.ExecuteAsync(null));
    }
}
