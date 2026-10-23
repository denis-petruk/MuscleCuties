namespace MuscleCuties.Core.Models.Nutrition.Planning;

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

    public static ProfileNutritionGoals FromPlan(NutritionPlan plan)
    {
        return FromCalculated(
            plan.Calories,
            plan.Protein,
            plan.Carbs,
            plan.Fats,
            plan.Fiber,
            plan.WaterLiters);
    }

    public static ProfileNutritionGoals FromCalculated(
        float calories,
        float protein,
        float carbs,
        float fats,
        float fiber,
        float waterLiters)
    {
        return new ProfileNutritionGoals(
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
    }

    public ProfileNutritionGoals WithFallbacks(NutritionPlan plan)
    {
        return WithFallbacks(FromPlan(plan));
    }

    public ProfileNutritionGoals WithFallbacks(ProfileNutritionGoals fallback)
    {
        return new ProfileNutritionGoals(
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
    }

    public ProfileNutritionGoals OnlyOverridesFrom(ProfileNutritionGoals calculated)
    {
        return new ProfileNutritionGoals(
            OverrideOrNull(Calories, calculated.Calories),
            OverrideOrNull(Protein, calculated.Protein),
            OverrideOrNull(Carbs, calculated.Carbs),
            OverrideOrNull(Fats, calculated.Fats),
            OverrideOrNull(Fiber, calculated.Fiber),
            OverrideOrNull(WaterLiters, calculated.WaterLiters),
            OverrideOrNull(Iron, calculated.Iron),
            OverrideOrNull(VitaminB12, calculated.VitaminB12),
            OverrideOrNull(VitaminC, calculated.VitaminC),
            OverrideOrNull(VitaminD, calculated.VitaminD),
            OverrideOrNull(VitaminA, calculated.VitaminA),
            OverrideOrNull(VitaminB6, calculated.VitaminB6),
            OverrideOrNull(Folate, calculated.Folate),
            OverrideOrNull(Calcium, calculated.Calcium),
            OverrideOrNull(Magnesium, calculated.Magnesium),
            OverrideOrNull(Zinc, calculated.Zinc),
            OverrideOrNull(Potassium, calculated.Potassium));
    }

    private static float UseValue(float? value, float? fallback)
    {
        if (value is > 0f && float.IsFinite(value.Value))
            return value.Value;

        return fallback is > 0f && float.IsFinite(fallback.Value) ? fallback.Value : 0f;
    }

    private static float? OverrideOrNull(float? value, float? calculated)
    {
        if (value is not > 0f || !float.IsFinite(value.Value))
            return null;
        return calculated is > 0f && float.IsFinite(calculated.Value) &&
               MathF.Abs(value.Value - calculated.Value) < 0.01f
            ? null
            : value;
    }
}
