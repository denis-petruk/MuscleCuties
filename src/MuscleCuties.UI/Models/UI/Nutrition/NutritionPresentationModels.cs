
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
    public string CaloriesText => DisplayFormat.Calories(Calories);
    public string MacrosText => DisplayFormat.Macros(Protein, Carbs, Fats);
    public string MacroText => DisplayFormat.Macros(Protein, Carbs, Fats);
    public string NutritionText => $"{DisplayFormat.Calories(Calories)} · {DisplayFormat.Macros(Protein, Carbs, Fats)}";
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
