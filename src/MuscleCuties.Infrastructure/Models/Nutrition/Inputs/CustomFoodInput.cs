namespace MuscleCuties.Core.Models.Nutrition.Inputs;

public sealed record CustomFoodInput(
    string Name,
    float ServingAmount,
    string ServingUnit,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string? CompanySource = null,
    float? Iron = null,
    float? VitaminB12 = null,
    float? VitaminD = null,
    float? Calcium = null);
