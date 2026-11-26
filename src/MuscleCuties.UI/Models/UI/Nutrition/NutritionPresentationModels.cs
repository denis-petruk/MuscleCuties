
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>Calories and macros together with the calories each macro contributes, which is what the</summary>
/// breakdown bar needs and what the DTO does not carry.</summary>
public readonly record struct NutritionMacros(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float ProteinCalories,
    float CarbsCalories,
    float FatsCalories)
{
    /// <summary>Calories with their unit.</summary>
    public string CaloriesText => DisplayFormat.Calories(Calories);
    /// <summary>Protein, carbohydrate and fat formatted as one line.</summary>
    public string MacrosText => DisplayFormat.Macros(Protein, Carbs, Fats);
    /// <summary>Same macro line, under the name the meal card binds to.</summary>
    public string MacroText => DisplayFormat.Macros(Protein, Carbs, Fats);
    /// <summary>Calories and macros as one line, the compact form most rows use.</summary>
    public string NutritionText => $"{DisplayFormat.Calories(Calories)} · {DisplayFormat.Macros(Protein, Carbs, Fats)}";
}

/// <summary>UI-local coverage statement; the presentation layer never binds a use-case DTO.</summary>
public readonly record struct MicronutrientCoverage(
    string Status,
    int CoveredItemCount,
    int UncoveredItemCount,
    IReadOnlyList<string> UnavailableItemNames);

/// <summary>The calorie and macro budget a meal slot is generated against.</summary>
public readonly record struct NutritionMealTarget(
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
