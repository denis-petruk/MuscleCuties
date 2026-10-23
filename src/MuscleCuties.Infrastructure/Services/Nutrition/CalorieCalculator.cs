using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public class CalorieCalculator : ICalorieCalculator
{
    public float CalculateBmr(float weightKg, float heightCm, int age)
    {
        return NutritionEnergyRule.CalculateBmr(weightKg, heightCm, age);
    }

    public float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek)
    {
        return NutritionEnergyRule.ApplyActivityMultiplier(bmr, workoutDaysPerWeek);
    }

    public float AdjustForGoal(float tdee, int userGoal, int weightGoalPace)
    {
        return NutritionEnergyRule.AdjustForGoal(tdee, userGoal, weightGoalPace);
    }

    public float AdjustForPhase(float calories, int cyclePhase)
    {
        return NutritionEnergyRule.AdjustForPhase(calories, cyclePhase);
    }

    public float Clamp(float value, float min = 1200f, float max = 4000f)
    {
        return NutritionEnergyRule.Clamp(value, min, max);
    }

    public (float Protein, float Carbs, float Fats) CalculateMacros(float targetCalories, float weightKg)
    {
        return NutritionEnergyRule.CalculateMacros(targetCalories, weightKg);
    }

    public static float GetActivityMultiplier(int workoutDaysPerWeek)
    {
        return NutritionEnergyRule.GetActivityMultiplier(workoutDaysPerWeek);
    }
}
