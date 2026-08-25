using System.Text.Json;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public sealed record ProfileNutritionGoals(
    float? Calories,
    float? Protein,
    float? Carbs,
    float? Fats,
    float? Fiber,
    float? WaterLiters,
    float? Iron,
    float? VitaminB12,
    float? VitaminC,
    float? VitaminD,
    float? VitaminA,
    float? VitaminB6,
    float? Folate,
    float? Calcium,
    float? Magnesium,
    float? Zinc,
    float? Potassium)
{
    public bool HasAnyValue =>
        Calories is > 0f ||
        Protein is > 0f ||
        Carbs is > 0f ||
        Fats is > 0f ||
        Fiber is > 0f ||
        WaterLiters is > 0f ||
        Iron is > 0f ||
        VitaminB12 is > 0f ||
        VitaminC is > 0f ||
        VitaminD is > 0f ||
        VitaminA is > 0f ||
        VitaminB6 is > 0f ||
        Folate is > 0f ||
        Calcium is > 0f ||
        Magnesium is > 0f ||
        Zinc is > 0f ||
        Potassium is > 0f;

    public static ProfileNutritionGoals Empty { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public static ProfileNutritionGoals FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Empty;

        try
        {
            return JsonSerializer.Deserialize<ProfileNutritionGoals>(json) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public static ProfileNutritionGoals FromPlan(NutritionPlan plan) =>
        FromCalculated(
            plan.Calories,
            plan.Protein,
            plan.Carbs,
            plan.Fats,
            plan.Fiber,
            plan.WaterLiters);

    public static ProfileNutritionGoals FromCalculated(
        float calories,
        float protein,
        float carbs,
        float fats,
        float fiber,
        float waterLiters) =>
        new(
            calories,
            protein,
            carbs,
            fats,
            fiber,
            waterLiters,
            DefaultIron,
            DefaultVitaminB12,
            DefaultVitaminC,
            DefaultVitaminD,
            DefaultVitaminA,
            DefaultVitaminB6,
            DefaultFolate,
            DefaultCalcium,
            DefaultMagnesium,
            DefaultZinc,
            DefaultPotassium);

    public ProfileNutritionGoals WithFallbacks(NutritionPlan plan) =>
        WithFallbacks(FromPlan(plan));

    public ProfileNutritionGoals WithFallbacks(ProfileNutritionGoals fallback) =>
        new(
            UseValue(Calories, fallback.Calories),
            UseValue(Protein, fallback.Protein),
            UseValue(Carbs, fallback.Carbs),
            UseValue(Fats, fallback.Fats),
            UseValue(Fiber, fallback.Fiber),
            UseValue(WaterLiters, fallback.WaterLiters),
            UseValue(Iron, fallback.Iron),
            UseValue(VitaminB12, fallback.VitaminB12),
            UseValue(VitaminC, fallback.VitaminC),
            UseValue(VitaminD, fallback.VitaminD),
            UseValue(VitaminA, fallback.VitaminA),
            UseValue(VitaminB6, fallback.VitaminB6),
            UseValue(Folate, fallback.Folate),
            UseValue(Calcium, fallback.Calcium),
            UseValue(Magnesium, fallback.Magnesium),
            UseValue(Zinc, fallback.Zinc),
            UseValue(Potassium, fallback.Potassium));

    public string ToJson() => JsonSerializer.Serialize(this);

    private const float DefaultIron = 18f;
    private const float DefaultVitaminB12 = 2.4f;
    private const float DefaultVitaminC = 75f;
    private const float DefaultVitaminD = 15f;
    private const float DefaultVitaminA = 700f;
    private const float DefaultVitaminB6 = 1.3f;
    private const float DefaultFolate = 400f;
    private const float DefaultCalcium = 1000f;
    private const float DefaultMagnesium = 320f;
    private const float DefaultZinc = 8f;
    private const float DefaultPotassium = 2600f;

    private static float UseValue(float? value, float? fallback) =>
        value is > 0f ? value.Value : fallback is > 0f ? fallback.Value : 0f;
}
