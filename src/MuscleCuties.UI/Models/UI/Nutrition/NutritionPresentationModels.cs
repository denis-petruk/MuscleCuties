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

public readonly record struct NutritionMealTarget(
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
