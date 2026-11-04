using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

public partial class WorkoutHistoryPage : ContentPage
{
    private readonly WorkoutHistoryViewModel _viewModel;

    public WorkoutHistoryPage(WorkoutHistoryViewModel viewModel)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, viewModel);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => _viewModel.InitializeAsync());
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
