using System.Diagnostics;
using System.Globalization;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void ToggleCustomFoodPanel()
    {
        InvalidateFoodSearch();
        _editingIngredient = null;
        SelectedFoodResult = null;
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
            !TryParseOptionalMacro(CustomFoodFats, out var fats) ||
            !TryParseOptionalNutrient(CustomFoodIron, out var iron) ||
            !TryParseOptionalNutrient(CustomFoodVitaminB12, out var vitaminB12) ||
            !TryParseOptionalNutrient(CustomFoodVitaminD, out var vitaminD) ||
            !TryParseOptionalNutrient(CustomFoodCalcium, out var calcium))
        {
            AddFoodMessage = "Enter valid custom food nutrition.";
            return;
        }

        IsBusy = true;
        try
        {
            var food = await _overviewUseCase.CreateCustomFoodAsync(new CreateCustomFoodRequest(
                CustomFoodName,
                servingAmount,
                SelectedCustomFoodServingUnit,
                calories,
                protein,
                carbs,
                fats,
                CustomFoodCompanySource,
                iron,
                vitaminB12,
                vitaminD,
                calcium));

            var item = CreateFoodSearchResultItem(food);
            SelectedFoodResult = item;
            FoodSearchResults = [];
            ResetFoodSearchPaging();
            IsCustomFoodPanelVisible = false;
            FoodGrams = "1";
            ClearCustomFoodForm();
            AddFoodMessage = $"{item.Name} saved. Add it to this meal when ready.";
        }
        catch (ArgumentException)
        {
            AddFoodMessage = "Check the food values and try again.";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NutritionViewModel] Custom food save failed ({ex.GetType().Name}).");
            AddFoodMessage = "Couldn't save this food. Your entries are still here; try again.";
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
        CustomFoodCompanySource = string.Empty;
        CustomFoodIron = string.Empty;
        CustomFoodVitaminB12 = string.Empty;
        CustomFoodVitaminD = string.Empty;
        CustomFoodCalcium = string.Empty;
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

        return float.IsFinite(amount) && amount >= 0f;
    }

    private static bool TryParseOptionalNutrient(string value, out float? amount)
    {
        amount = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) &&
            !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return false;
        if (!float.IsFinite(parsed) || parsed < 0f)
            return false;
        amount = parsed;
        return true;
    }
}
