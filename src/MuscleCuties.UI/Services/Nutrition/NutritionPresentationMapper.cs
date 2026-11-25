using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
/// The only UI adapter allowed to know Core nutrition value contracts. Views and view models
/// consume the resulting UI-local models, so presentation formatting cannot spread Core types
/// through the rest of the client.
/// </summary>
public static partial class NutritionPresentationMapper
{
    /// <summary>The macros from the four totals.</summary>
    public static NutritionMacros FromTotals(float calories, float protein, float carbs, float fats) =>
        FromMacroNutrients(new MacroNutrients(calories, protein, carbs, fats));

    /// <summary>The macros scaled from per-100-gram values.</summary>
    public static NutritionMacros FromPer100Grams(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams) =>
        FromMacroNutrients(MacroNutrients.FromPer100g(calories, protein, carbs, fats, grams));

    /// <summary>The nutrition line for the given grams.</summary>
    public static string NutritionTextForGrams(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams) =>
        FromPer100Grams(calories, protein, carbs, fats, grams).NutritionText;

    /// <summary>The meal target from its values.</summary>
    public static NutritionMealTarget FromMealTargetValues(
        float calories,
        float protein,
        float carbs,
        float fats) =>
        new(calories, protein, carbs, fats);

    /// <summary>The macros from the contract's nutrients.</summary>
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
