using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.ViewModels.Nutrition;

namespace MuscleCuties.UI.Pages.Nutrition;

public partial class FoodLogPage : ContentPage, IQueryAttributable
{
    private readonly FoodLogViewModel _viewModel;
    private Guid? _pendingEntry;
    private string? _pendingAddMeal;
    private int? _pendingAddMinuteOfDay;

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
        if (NutritionNavigation.TryReadEntryId(query, out var entryId))
            _pendingEntry = entryId;

        // Sent by the Nutrition meal-entry sheet: which meal to add food to, and when.
        // The page only carries the intent; the view model decides what it means.
        if (NutritionNavigation.TryReadAddFoodIntent(query, out var mealName, out var minuteOfDay))
        {
            _pendingAddMeal = mealName;
            _pendingAddMinuteOfDay = minuteOfDay;
        }
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

            if (!string.IsNullOrWhiteSpace(_pendingAddMeal))
            {
                var mealIntent = _pendingAddMeal;
                var timeIntent = _pendingAddMinuteOfDay;
                _pendingAddMeal = null;
                _pendingAddMinuteOfDay = null;
                await _viewModel.OpenAddFoodIntentAsync(mealIntent, timeIntent);
            }
        });
    }
}
