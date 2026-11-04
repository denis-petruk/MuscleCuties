namespace MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

public sealed record CreateCustomFoodRequest(
    string Name,
    float ServingAmount,
    string ServingUnit,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string? CompanySource = null,
    float? IronMilligrams = null,
    float? VitaminB12Micrograms = null,
    float? VitaminDMicrograms = null,
    float? CalciumMilligrams = null);

public sealed record CustomFoodDto(
    string FoodKey,
    string Name,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float ServingSize,
    string ServingSizeUnit,
    string? CompanySource = null,
    float? IronMilligrams = null,
    float? VitaminB12Micrograms = null,
    float? VitaminDMicrograms = null,
    float? CalciumMilligrams = null);

public sealed record CustomFoodRecord(
    Guid Id,
    string Name,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float ServingSize,
    string ServingSizeUnit,
    string? CompanySource = null,
    float? IronMilligrams = null,
    float? VitaminB12Micrograms = null,
    float? VitaminDMicrograms = null,
    float? CalciumMilligrams = null);
