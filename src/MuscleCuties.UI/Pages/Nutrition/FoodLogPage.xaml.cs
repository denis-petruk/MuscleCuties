using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Nutrition;

namespace MuscleCuties.UI.Pages.Nutrition;

public partial class FoodLogPage : ContentPage, IQueryAttributable
{
    private readonly FoodLogViewModel _viewModel;
    private Guid? _pendingEntry;

    public FoodLogPage(FoodLogViewModel viewModel)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, viewModel);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(EditSheetSurface, height);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("entryId", out var value) && Guid.TryParse(value?.ToString(), out var id))
            _pendingEntry = id;
    }

    protected override void OnDisappearing()
    {
        _viewModel.Deactivate();
        base.OnDisappearing();
    }

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
            }
        });
    }
}
