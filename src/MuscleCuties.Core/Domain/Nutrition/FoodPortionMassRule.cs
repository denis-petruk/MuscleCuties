namespace MuscleCuties.Core.Domain.Nutrition;

public static class FoodPortionMassRule
{
    public const float OneGram = 1f;
    public const float GramsPerKilogram = 1000f;
    public const float MilligramsPerGram = 1000f;
    public const float GramsPerOunce = 28.3495f;
    public const float GramsPerPound = 453.592f;
    public const float NoGrams = 0f;
    public const string GramUnit = "g";
    public const string OunceUnit = "oz";
    public const string PoundUnit = "lb";

    public static bool IsValidGrams(float grams) => float.IsFinite(grams) && grams > NoGrams;

    public static bool TryConvertMassToGrams(float amount, string? unit, out float grams)
    {
        grams = NoGrams;
        if (!float.IsFinite(amount) || amount <= NoGrams || string.IsNullOrWhiteSpace(unit))
            return false;

        var normalized = unit.Trim().ToLowerInvariant().Replace(".", string.Empty);
        grams = normalized switch
        {
            GramUnit or "gram" or "grams" or "gm" => amount,
            "kg" or "kilogram" or "kilograms" or "kgs" => amount * GramsPerKilogram,
            "mg" or "milligram" or "milligrams" => amount / MilligramsPerGram,
            OunceUnit or "ounce" or "ounces" => amount * GramsPerOunce,
            PoundUnit or "lbs" or "pound" or "pounds" => amount * GramsPerPound,
            _ => NoGrams
        };

        return IsValidGrams(grams);
    }
}
