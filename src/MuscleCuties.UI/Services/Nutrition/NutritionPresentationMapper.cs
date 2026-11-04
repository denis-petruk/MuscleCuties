using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
/// The only UI adapter allowed to know Core nutrition value contracts. Views and view models
/// consume the resulting UI-local models, so presentation formatting cannot spread Core types
/// through the rest of the client.
/// </summary>
public static partial class NutritionPresentationMapper
{
    public static NutritionMacros FromTotals(float calories, float protein, float carbs, float fats) =>
        FromMacroNutrients(new MacroNutrients(calories, protein, carbs, fats));

    public static NutritionMacros FromPer100Grams(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams) =>
        FromMacroNutrients(MacroNutrients.FromPer100g(calories, protein, carbs, fats, grams));

    public static string NutritionTextForGrams(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams) =>
        FromPer100Grams(calories, protein, carbs, fats, grams).NutritionText;

    public static NutritionMealTarget FromMealTargetValues(
        float calories,
        float protein,
        float carbs,
        float fats) =>
        new(calories, protein, carbs, fats);

    private static NutritionMacros FromMacroNutrients(MacroNutrients macros) =>
        new(
            macros.Calories,
            macros.Protein,
            macros.Carbs,
            macros.Fats,
            macros.ProteinCalories,
            macros.CarbsCalories,
            macros.FatsCalories);
}
