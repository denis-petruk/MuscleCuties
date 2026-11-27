// Food source tabs and result paging.
// Second half of NutritionViewModel; see its declaration for the full contract.

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The food finder's three sources — catalog search, barcode and a custom food — and the tab state that shows</summary>
/// which one is active.</summary>
public partial class NutritionViewModel
{
    /// <summary>Shows the search food source.</summary>
    [RelayCommand]
    private void SelectSearchFoodSource() => SelectFoodSource(MealFoodSource.Search);

    /// <summary>Shows the barcode food source.</summary>
    [RelayCommand]
    private void SelectBarcodeFoodSource() => SelectFoodSource(MealFoodSource.Barcode);

    /// <summary>Shows the custom food source.</summary>
    [RelayCommand]
    private void SelectCustomFoodSource() => SelectFoodSource(MealFoodSource.Custom);

    /// <summary>Switches source, stopping a barcode scan that belongs to the tab being left.</summary>
    private void SelectFoodSource(MealFoodSource source)
    {
        if (IsBusy)
            return;

        BarcodeScan.Cancel();
        _editingIngredient = null;
        SelectedFoodResult = null;
        SelectedFoodSource = source;
        IsFoodFinderExpanded = true;
        IsCustomFoodPanelVisible = source == MealFoodSource.Custom;
        AddFoodMessage = string.Empty;

        UpdateFoodSourceTabs(source);

        if (source == MealFoodSource.Barcode)
        {
            BarcodeInputText = string.Empty;
            BarcodeScanStatus = UiTextCatalog.Get("Nutrition_Barcode_StatusEnterDigits");
        }
        else
        {
            BarcodeScanStatus = string.Empty;
        }

        NotifyFoodFinderProperties();
    }

    /// <summary>Returns the finder to catalog search with no barcode left behind, used when the editor closes.</summary>
    private void ResetFoodSource()
    {
        BarcodeScan.Cancel();
        SelectedFoodSource = MealFoodSource.Search;
        UpdateFoodSourceTabs(MealFoodSource.Search);
        BarcodeInputText = string.Empty;
        BarcodeScanStatus = string.Empty;
    }

    /// <summary>Marks the one active tab, so the three sources cannot appear selected at once.</summary>
    private void UpdateFoodSourceTabs(MealFoodSource selectedSource)
    {
        SearchFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Search;
        BarcodeFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Barcode;
        CustomFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Custom;
    }
}
