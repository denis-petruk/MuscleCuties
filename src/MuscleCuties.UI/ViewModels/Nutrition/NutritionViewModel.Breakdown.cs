using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void OpenDailyBreakdown()
    {
        Breakdown.Show(
            UiTextCatalog.Get("Nutrition_Breakdown_DailyTitle"),
            NutritionPresentationMapper.FromTotals(ConsumedCalories, ConsumedProtein, ConsumedCarbs, ConsumedFats),
            Micronutrients,
            null,
            SelectedBreakdownCoverageText);
        IsBreakdownModalVisible = true;
    }

    private void OpenMealBreakdown(MealItem? meal)
    {
        if (meal is null)
            return;

        Breakdown.Show(
            $"{meal.MealType} breakdown",
            meal.Macros,
            meal.Micronutrients,
            meal);
        IsBreakdownModalVisible = true;
    }

    private void CloseBreakdownModal()
    {
        IsBreakdownModalVisible = false;
        Breakdown.Clear();
    }

    /// <summary>Used by the shared presenter when a meal-level breakdown offers Edit meal.</summary>
    private Task EditBreakdownMealAsync(MealItem meal)
    {
        IsBreakdownModalVisible = false;
        return EditMealAsync(meal);
    }

    /// <summary>
    /// NM03: the note states what the micronutrient totals cover. Presentation parity alone never
    /// claims data coverage, and a missing additive field shows no note for older responses.
    /// </summary>
    private static string DescribeMicronutrientCoverage(MicronutrientCoverage? coverage) =>
        NutritionBreakdownPresenter.DescribeCoverage(coverage);
}
