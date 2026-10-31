namespace MuscleCuties.Core.Domain.Nutrition;

public static class NutritionEnergyRule
{
    private const float MinimumWeightKg = 30f;
    private const float MaximumWeightKg = 500f;
    private const float MinimumHeightCm = 100f;
    private const float MaximumHeightCm = 300f;
    private const int MinimumAge = 18;
    private const int MaximumAge = 100;

    private const float WeightCoefficientPerKg = 10f;
    private const float HeightCoefficientPerCm = 6.25f;
    private const float AgeCoefficientPerYear = 5f;
    private const float FemaleBmrConstantKcal = 161f;

    private const int ZeroWorkoutDays = 0;
    private const int LightActivityMaxWorkoutDays = 2;
    private const int ModerateActivityMaxWorkoutDays = 4;
    private const int HighActivityMaxWorkoutDays = 6;
    private const float SedentaryMultiplier = 1.2f;
    private const float LightActivityMultiplier = 1.375f;
    private const float ModerateActivityMultiplier = 1.55f;
    private const float HighActivityMultiplier = 1.725f;
    private const float VeryHighActivityMultiplier = 1.8f;

    private const int AggressiveWeightGoalPace = 1;
    private const float AggressiveGoalDeltaKcal = 500f;
    private const float ModerateGoalDeltaKcal = 300f;
    private const int FatLossGoal = 0;
    private const int MuscleGainGoal = 2;

    private const int MenstrualPhase = 0;
    private const int OvulatoryPhase = 2;
    private const int LutealPhase = 3;
    private const float MenstrualCalorieAdjustmentKcal = -100f;
    private const float OvulatoryCalorieAdjustmentKcal = 50f;
    private const float LutealCalorieAdjustmentKcal = 150f;

    private const float MinimumCalorieTargetKcal = 1200f;
    private const float MaximumCalorieTargetKcal = 4000f;
    private const float MaximumMacroCalorieTargetKcal = 10000f;
    private const float ProteinGramsPerKg = 1.8f;
    private const float FatCalorieShare = 0.25f;
    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramCarb = 4f;
    private const float CaloriesPerGramFat = 9f;

    public static float CalculateBmr(float weightKg, float heightCm, int age)
    {
        if (!float.IsFinite(weightKg) || !float.IsFinite(heightCm) ||
            weightKg is < MinimumWeightKg or > MaximumWeightKg ||
            heightCm is < MinimumHeightCm or > MaximumHeightCm ||
            age is < MinimumAge or > MaximumAge)
            return 0f;
        return WeightCoefficientPerKg * weightKg + HeightCoefficientPerCm * heightCm - AgeCoefficientPerYear * age - FemaleBmrConstantKcal;
    }

    public static float GetActivityMultiplier(int workoutDaysPerWeek)
    {
        return workoutDaysPerWeek switch
        {
            <= ZeroWorkoutDays => SedentaryMultiplier,
            <= LightActivityMaxWorkoutDays => LightActivityMultiplier,
            <= ModerateActivityMaxWorkoutDays => ModerateActivityMultiplier,
            <= HighActivityMaxWorkoutDays => HighActivityMultiplier,
            _ => VeryHighActivityMultiplier
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
        var delta = weightGoalPace == AggressiveWeightGoalPace
            ? AggressiveGoalDeltaKcal
            : ModerateGoalDeltaKcal;
        return userGoal switch
        {
            FatLossGoal => tdee - delta,
            MuscleGainGoal => tdee + delta,
            _ => tdee
        };
    }

    public static float AdjustForPhase(float calories, int cyclePhase)
    {
        if (!float.IsFinite(calories) || calories <= 0f)
            return 0f;
        return calories + cyclePhase switch
        {
            MenstrualPhase => MenstrualCalorieAdjustmentKcal,
            OvulatoryPhase => OvulatoryCalorieAdjustmentKcal,
            LutealPhase => LutealCalorieAdjustmentKcal,
            _ => 0f
        };
    }

    public static float Clamp(float value, float min = MinimumCalorieTargetKcal, float max = MaximumCalorieTargetKcal)
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
            targetCalories is <= 0f or > MaximumMacroCalorieTargetKcal ||
            weightKg is < MinimumWeightKg or > MaximumWeightKg)
            return (0f, 0f, 0f);
        var protein = weightKg * ProteinGramsPerKg;
        var fats = targetCalories * FatCalorieShare / CaloriesPerGramFat;
        var carbs = (targetCalories - protein * CaloriesPerGramProtein - fats * CaloriesPerGramFat) /
                    CaloriesPerGramCarb;
        return (MathF.Max(protein, 0f), MathF.Max(carbs, 0f), MathF.Max(fats, 0f));
    }
}
