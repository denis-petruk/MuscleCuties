using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _viewModel;

    public WorkoutPage(WorkoutViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (BindingContext is null)
            BindingContext = _viewModel;
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.CloseInjuryModalCommand.Execute(null);
        base.OnNavigatedFrom(args);
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsInjuryModalVisible)
            return base.OnBackButtonPressed();

        _viewModel.CloseInjuryModalCommand.Execute(null);
        return true;
    }
}
