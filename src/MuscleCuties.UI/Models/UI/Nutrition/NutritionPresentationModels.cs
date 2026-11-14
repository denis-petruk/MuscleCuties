using MuscleCuties.UI.Services.Formatting;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public readonly record struct NutritionMacros(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float ProteinCalories,
    float CarbsCalories,
    float FatsCalories)
{
    public string CaloriesText => NutritionFormat.Calories(Calories);
    public string MacrosText => NutritionFormat.Macros(Protein, Carbs, Fats);
    public string MacroText => NutritionFormat.Macros(Protein, Carbs, Fats);
    public string NutritionText => $"{NutritionFormat.Calories(Calories)} · {NutritionFormat.Macros(Protein, Carbs, Fats)}";
}

/// <summary>UI-local coverage statement; the presentation layer never binds a use-case DTO.</summary>
public readonly record struct MicronutrientCoverage(
    string Status,
    int CoveredItemCount,
    int UncoveredItemCount,
    IReadOnlyList<string> UnavailableItemNames);

public readonly record struct NutritionMealTarget(
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
