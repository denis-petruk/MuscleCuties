using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.ViewModels.Nutrition;

namespace MuscleCuties.UI.Pages.Nutrition;

/// <summary>
///     Daily food diary: the logged timeline, water, notes, and the edit sheet.
/// </summary>
public partial class FoodLogPage : ContentPage, IQueryAttributable
{
    private readonly FoodLogViewModel _viewModel;
    private Guid? _pendingEntry;

    /// <summary>Initializes the page and its view model.</summary>
    public FoodLogPage(FoodLogViewModel viewModel)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, viewModel);
    }

    /// <summary>Adjusts the layout when the page is resized.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(EditSheetSurface, height);
    }

    /// <summary>Reads the entry the diary was opened for, so an edit request survives navigation into the page.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (NutritionNavigation.TryReadEntryId(query, out var entryId))
            _pendingEntry = entryId;
    }

    /// <summary>Stops the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _viewModel.Deactivate();
        base.OnDisappearing();
    }

    /// <summary>Reloads the log when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(async () =>
        {
            await _viewModel.InitializeAsync();
            if (_pendingEntry is Guid id)
            {
                _pendingEntry = null;
                await _viewModel.OpenRecordAsync(id);
            }        });
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.Breakdown.IsVisible)
        {
            _viewModel.Breakdown.CloseCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsEditModalVisible)
        {
            _viewModel.CloseEditModalCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
