using MuscleCuties.UI.ViewModels.Workout;

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
}
