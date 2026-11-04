namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Validates custom-food inputs and converts their nutrition to a per-100g basis.</summary>
public static class CustomFoodCreationRule
{
    private const float GramsPerNutritionBasis = 100f;
    public static bool TryCreate(
        string? name,
        float servingAmount,
        string? servingUnit,
        float calories,
        float protein,
        float carbs,
        float fats,
        out CustomFoodNutrition nutrition,
        out string error)
    {
        nutrition = default;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Enter a food name.";
            return false;
        }
        if (!float.IsFinite(calories) || calories <= 0f)
        {
            error = "Enter calories greater than zero.";
            return false;
        }
        if (!float.IsFinite(protein) || !float.IsFinite(carbs) || !float.IsFinite(fats) ||
            protein < 0f || carbs < 0f || fats < 0f)
        {
            error = "Macros must be finite and cannot be negative.";
            return false;
        }
        if (!FoodPortionMassRule.TryConvertMassToGrams(servingAmount, servingUnit, out var servingGrams))
        {
            error = "Use a serving unit that can be converted to grams.";
            return false;
        }

        var perHundredGrams = GramsPerNutritionBasis / servingGrams;
        var convertedCalories = calories * perHundredGrams;
        var convertedProtein = protein * perHundredGrams;
        var convertedCarbs = carbs * perHundredGrams;
        var convertedFats = fats * perHundredGrams;
        if (!float.IsFinite(convertedCalories) || !float.IsFinite(convertedProtein) ||
            !float.IsFinite(convertedCarbs) || !float.IsFinite(convertedFats) ||
            (calories > 0f && convertedCalories == 0f) ||
            (protein > 0f && convertedProtein == 0f) ||
            (carbs > 0f && convertedCarbs == 0f) ||
            (fats > 0f && convertedFats == 0f) ||
            !CanStoreAsDecimal(convertedCalories) || !CanStoreAsDecimal(convertedProtein) ||
            !CanStoreAsDecimal(convertedCarbs) || !CanStoreAsDecimal(convertedFats) ||
            !CanStoreAsDecimal(servingAmount))
        {
            error = "Nutrition values are outside the supported range.";
            return false;
        }

        nutrition = new CustomFoodNutrition(
            name.Trim(), convertedCalories, convertedProtein, convertedCarbs, convertedFats, servingAmount, servingUnit!.Trim(), servingGrams);
        return true;
    }

    public static bool IsValidRequest(
        string? name,
        float servingAmount,
        string? servingUnit,
        float calories,
        float protein,
        float carbs,
        float fats,
        float? ironMilligrams,
        float? vitaminB12Micrograms,
        float? vitaminDMicrograms,
        float? calciumMilligrams)
    {
        if (!TryCreate(name, servingAmount, servingUnit, calories, protein, carbs, fats,
                out var nutrition, out _))
            return false;

        return TryNormalizeMicronutrients(
            ironMilligrams,
            vitaminB12Micrograms,
            vitaminDMicrograms,
            calciumMilligrams,
            nutrition.ServingGrams,
            out _);
    }

    public static bool TryNormalizeMicronutrients(
        float? ironMilligrams,
        float? vitaminB12Micrograms,
        float? vitaminDMicrograms,
        float? calciumMilligrams,
        float servingGrams,
        out CustomFoodMicronutrients micronutrients)
    {
        micronutrients = default;
        if (!TryNormalizeOptionalMicronutrient(ironMilligrams, servingGrams, out var iron) ||
            !TryNormalizeOptionalMicronutrient(vitaminB12Micrograms, servingGrams, out var vitaminB12) ||
            !TryNormalizeOptionalMicronutrient(vitaminDMicrograms, servingGrams, out var vitaminD) ||
            !TryNormalizeOptionalMicronutrient(calciumMilligrams, servingGrams, out var calcium))
            return false;

        micronutrients = new CustomFoodMicronutrients(iron, vitaminB12, vitaminD, calcium);
        return true;
    }

    public static bool TryNormalizeOptionalMicronutrient(float? value, float servingGrams, out decimal? perHundredGrams)
    {
        perHundredGrams = null;
        if (!value.HasValue)
            return true;
        if (!float.IsFinite(value.Value) || value.Value < 0f ||
            !float.IsFinite(servingGrams) || servingGrams <= 0f)
            return false;
        var normalized = value.Value * GramsPerNutritionBasis / servingGrams;
        if (!float.IsFinite(normalized) || !CanStoreAsDecimal(normalized))
            return false;
        perHundredGrams = (decimal)normalized;
        return true;
    }

    private static bool CanStoreAsDecimal(float value) =>
        Math.Abs((double)value) <= (double)decimal.MaxValue &&
        (value == 0f || (decimal)value != 0m);
}

public readonly record struct CustomFoodNutrition(
    string Name,
    float CaloriesPer100Grams,
    float ProteinPer100Grams,
    float CarbsPer100Grams,
    float FatsPer100Grams,
    float ServingAmount,
    string ServingUnit,
    float ServingGrams);

public readonly record struct CustomFoodMicronutrients(
    decimal? IronMilligramsPer100Grams,
    decimal? VitaminB12MicrogramsPer100Grams,
    decimal? VitaminDMicrogramsPer100Grams,
    decimal? CalciumMilligramsPer100Grams);
