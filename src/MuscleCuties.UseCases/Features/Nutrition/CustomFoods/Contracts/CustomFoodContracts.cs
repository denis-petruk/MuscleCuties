namespace MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

/// <summary>A food she is creating, with the values she typed.</summary>
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

/// <summary>A food she created, as the client shows it.</summary>
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

/// <summary>A food she created as stored, including its per-100 g values.</summary>
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
