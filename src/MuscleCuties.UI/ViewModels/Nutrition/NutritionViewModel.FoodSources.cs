using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    [RelayCommand]
    private void SelectSearchFoodSource() => SelectFoodSource(MealFoodSource.Search);

    [RelayCommand]
    private void SelectBarcodeFoodSource() => SelectFoodSource(MealFoodSource.Barcode);

    [RelayCommand]
    private void SelectCustomFoodSource() => SelectFoodSource(MealFoodSource.Custom);

    private void SelectFoodSource(MealFoodSource source)
    {
        if (IsBusy)
            return;

        CancelBarcodeCameraCapture();
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

    private void ResetFoodSource()
    {
        CancelBarcodeCameraCapture();
        SelectedFoodSource = MealFoodSource.Search;
        UpdateFoodSourceTabs(MealFoodSource.Search);
        BarcodeInputText = string.Empty;
        BarcodeScanStatus = string.Empty;
    }

    private void UpdateFoodSourceTabs(MealFoodSource selectedSource)
    {
        SearchFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Search;
        BarcodeFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Barcode;
        CustomFoodSourceTab.IsSelected = selectedSource == MealFoodSource.Custom;
    }
}