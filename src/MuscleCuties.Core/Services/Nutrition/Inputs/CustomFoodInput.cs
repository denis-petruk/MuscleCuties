namespace MuscleCuties.Core.Services.Nutrition.Inputs;

public sealed record CustomFoodInput(
    string Name,
    float ServingAmount,
    string ServingUnit,
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
