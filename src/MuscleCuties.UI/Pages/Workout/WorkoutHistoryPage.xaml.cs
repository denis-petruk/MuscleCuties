using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Workout;

/// <summary>
///     Archive of completed sessions, grouped by week.
/// </summary>
public partial class WorkoutHistoryPage : ContentPage
{
    private readonly WorkoutHistoryViewModel _viewModel;

    /// <summary>Initializes the page and its view model.</summary>
    public WorkoutHistoryPage(WorkoutHistoryViewModel viewModel)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, viewModel);
    }

    /// <summary>Loads the history when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => _viewModel.InitializeAsync());
    }

    /// <summary>Adjusts the layout when the page is resized.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsDetailSheetVisible)
        {
            _viewModel.CloseSessionDetailCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
