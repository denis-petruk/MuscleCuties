namespace MuscleCuties.Core.Domain.Nutrition;

public static class NutritionEnergyRule
{
    public static float CalculateBmr(float weightKg, float heightCm, int age)
    {
        if (!float.IsFinite(weightKg) || !float.IsFinite(heightCm) ||
            weightKg is < 30f or > 500f || heightCm is < 100f or > 300f ||
            age is < 18 or > 100)
            return 0f;
        return 10f * weightKg + 6.25f * heightCm - 5f * age - 161f;
    }

    public static float GetActivityMultiplier(int workoutDaysPerWeek)
    {
        return workoutDaysPerWeek switch
        {
            <= 0 => 1.2f,
            <= 2 => 1.375f,
            <= 4 => 1.55f,
            <= 6 => 1.725f,
            _ => 1.8f
        };
    }

    public static float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek)
    {
        return float.IsFinite(bmr) && bmr > 0f ? bmr * GetActivityMultiplier(workoutDaysPerWeek) : 0f;
    }

    public static float AdjustForGoal(float tdee, int userGoal, int weightGoalPace)
    {
        if (!float.IsFinite(tdee) || tdee <= 0f)
            return 0f;
        var delta = weightGoalPace == 1 ? 500f : 300f;
        return userGoal switch
        {
            0 => tdee - delta,
            2 => tdee + delta,
            _ => tdee
        };
    }

    public static float AdjustForPhase(float calories, int cyclePhase)
    {
        if (!float.IsFinite(calories) || calories <= 0f)
            return 0f;
        return calories + cyclePhase switch
        {
            0 => -100f,
            2 => +50f,
            3 => +150f,
            _ => 0f
        };
    }

    public static float Clamp(float value, float min = 1200f, float max = 4000f)
    {
        if (!float.IsFinite(value) || !float.IsFinite(min) || !float.IsFinite(max) ||
            min <= 0f || max < min)
            return 0f;
        return Math.Clamp(value, min, max);
    }

    public static (float Protein, float Carbs, float Fats) CalculateMacros(
        float targetCalories, float weightKg)
    {
        if (!float.IsFinite(targetCalories) || !float.IsFinite(weightKg) ||
            targetCalories is <= 0f or > 10000f || weightKg is < 30f or > 500f)
            return (0f, 0f, 0f);
        var protein = weightKg * 1.8f;
        var fats = targetCalories * 0.25f / 9f;
        var carbs = (targetCalories - protein * 4f - fats * 9f) / 4f;
        return (MathF.Max(protein, 0f), MathF.Max(carbs, 0f), MathF.Max(fats, 0f));
    }
}
