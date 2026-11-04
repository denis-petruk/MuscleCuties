using System.Collections.ObjectModel;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Nutrition;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void OpenDailyBreakdown()
    {
        ApplyBreakdown(
            "Daily nutrition",
            NutritionPresentationMapper.FromTotals(ConsumedCalories, ConsumedProtein, ConsumedCarbs, ConsumedFats),
            Micronutrients,
            null);
    }

    private void OpenMealBreakdown(MealItem? meal)
    {
        if (meal is null)
            return;

        ApplyBreakdown(
            $"{meal.MealType} breakdown",
            meal.Macros,
            meal.Micronutrients,
            meal);
    }

    private void ApplyBreakdown(
        string title,
        NutritionMacros macros,
        IEnumerable<DailyMicronutrientItem> micronutrients,
        MealItem? meal)
    {
        var nutrients = micronutrients.ToList();

        SelectedBreakdownMeal = meal;
        SelectedBreakdownTitle = title;
        SelectedBreakdownCaloriesText = macros.CaloriesText;
        SelectedBreakdownMacrosText = macros.MacroText;
        SelectedBreakdownFiberText = BuildFiberText(nutrients);
        SelectedBreakdownNutrientSummaryText = BuildMicronutrientSummaryText(nutrients);
        SelectedBreakdownProteinCalories = macros.ProteinCalories;
        SelectedBreakdownCarbsCalories = macros.CarbsCalories;
        SelectedBreakdownFatsCalories = macros.FatsCalories;
        SelectedBreakdownMacroItems = new ObservableCollection<MacroBreakdownItem>(BuildMacroBreakdownItems(macros));
        SelectedBreakdownMicronutrients = new ObservableCollection<DailyMicronutrientItem>(nutrients);
        IsBreakdownModalVisible = true;
    }

    private void CloseBreakdownModal()
    {
        IsBreakdownModalVisible = false;
    }

    private async Task EditSelectedBreakdownMealAsync()
    {
        var meal = SelectedBreakdownMeal;
        if (meal is null)
            return;

        CloseBreakdownModal();
        await EditMealAsync(meal);
    }

    private static IEnumerable<MacroBreakdownItem> BuildMacroBreakdownItems(NutritionMacros macros)
    {
        var proteinCalories = macros.ProteinCalories;
        var carbsCalories = macros.CarbsCalories;
        var fatsCalories = macros.FatsCalories;
        var macroCalories = Math.Max(0f, proteinCalories + carbsCalories + fatsCalories);

        return
        [
            new MacroBreakdownItem
            {
                Name = "Protein",
                Grams = macros.Protein,
                Calories = proteinCalories,
                Progress = CalculateMacroShare(proteinCalories, macroCalories),
                Color = AppThemeResources.GetColor("ProteinLight", default)
            },
            new MacroBreakdownItem
            {
                Name = "Carbs",
                Grams = macros.Carbs,
                Calories = carbsCalories,
                Progress = CalculateMacroShare(carbsCalories, macroCalories),
                Color = AppThemeResources.GetColor("CarbsLight", default)
            },
            new MacroBreakdownItem
            {
                Name = "Fats",
                Grams = macros.Fats,
                Calories = fatsCalories,
                Progress = CalculateMacroShare(fatsCalories, macroCalories),
                Color = AppThemeResources.GetColor("FatsLight", default)
            }
        ];
    }

    private static float CalculateMacroShare(float calories, float totalCalories)
    {
        return totalCalories <= 0f ? 0f : Math.Clamp(calories / totalCalories, 0f, 1f);
    }

    private static string BuildFiberText(IEnumerable<DailyMicronutrientItem> micronutrients)
    {
        var fiber = micronutrients.FirstOrDefault(item => item.Name == "Fiber");
        return fiber is null ? "0.0g fiber" : $"{fiber.Amount:N1}g fiber";
    }

    private static string BuildMicronutrientSummaryText(List<DailyMicronutrientItem> micronutrients)
    {
        if (micronutrients.Count == 0)
            return "No micronutrients tracked yet";

        var complete = micronutrients.Count(item => item.IsGoalHit);
        return $"{complete} of {micronutrients.Count} daily targets reached";
    }
}
