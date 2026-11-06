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
    public string CaloriesText => $"{Calories:N0} kcal";
    public string MacrosText => $"P {Protein:N0}g  C {Carbs:N0}g  F {Fats:N0}g";
    public string MacroText => $"P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";
    public string NutritionText => $"{Calories:N0} kcal · P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";
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
