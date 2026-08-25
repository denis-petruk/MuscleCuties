using System.Globalization;
using MuscleCuties.Core.Services.Nutrition.Inputs;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void ToggleCustomFoodPanel()
    {
        IsFoodFinderExpanded = true;
        IsCustomFoodPanelVisible = !IsCustomFoodPanelVisible;
        AddFoodMessage = string.Empty;
    }

    private async Task CreateCustomFoodAsync()
    {
        if (!TryParseAmount(CustomFoodServingAmount, out var servingAmount) ||
            !TryParseAmount(CustomFoodCalories, out var calories) ||
            !TryParseOptionalMacro(CustomFoodProtein, out var protein) ||
            !TryParseOptionalMacro(CustomFoodCarbs, out var carbs) ||
            !TryParseOptionalMacro(CustomFoodFats, out var fats))
        {
            AddFoodMessage = "Enter valid custom food nutrition.";
            return;
        }

        IsBusy = true;
        try
        {
            var food = await _nutritionService.CreateCustomFoodAsync(new CustomFoodInput(
                CustomFoodName,
                servingAmount,
                SelectedCustomFoodServingUnit,
                calories,
                protein,
                carbs,
                fats));

            var item = CreateFoodSearchResultItem(food);
            SelectedFoodResult = item;
            FoodSearchResults = [];
            ResetFoodSearchPaging();
            IsCustomFoodPanelVisible = false;
            FoodGrams = "1";
            ClearCustomFoodForm();
            AddFoodMessage = $"{item.Name} saved. Add it to this meal when ready.";
        }
        catch (ArgumentException ex)
        {
            AddFoodMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearCustomFoodForm()
    {
        CustomFoodName = string.Empty;
        CustomFoodCalories = string.Empty;
        CustomFoodProtein = string.Empty;
        CustomFoodCarbs = string.Empty;
        CustomFoodFats = string.Empty;
        CustomFoodServingAmount = "100";
        SelectedCustomFoodServingUnit = "g";
    }

    private static bool TryParseOptionalMacro(string value, out float amount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            amount = 0f;
            return true;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) &&
            !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
        {
            amount = 0f;
            return false;
        }

        return amount >= 0f;
    }
}
