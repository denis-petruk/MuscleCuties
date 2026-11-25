using MuscleCuties.UI.Services.Nutrition;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The nutrition breakdown panel, opened either for the whole day or for one logged meal.</summary>
public partial class NutritionViewModel
{
    /// <summary>Shows the day's totals with their coverage line.</summary>
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

    /// <summary>Shows one meal's macros and micronutrients, with the meal attached so it can be edited from the panel.</summary>
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

    /// <summary>Hides the panel and empties it, so the next opening cannot show the previous breakdown.</summary>
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
