using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>The calorie maths: basal rate, activity, goal and phase, each step separate so a change can be made in one</summary>
/// place.</summary>
public class CalorieCalculator : ICalorieCalculator
{
    /// <summary>The basal metabolic rate from weight, height, age and sex.</summary>
    public float CalculateBmr(float weightKg, float heightCm, int age)
    {
        return NutritionEnergyRule.CalculateBmr(weightKg, heightCm, age);
    }

    /// <summary>The rate raised by the activity level, which is the step that most changes the number.</summary>
    public float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek)
    {
        return NutritionEnergyRule.ApplyActivityMultiplier(bmr, workoutDaysPerWeek);
    }

    /// <summary>The maintenance rate moved by the goal's deficit or surplus.</summary>
    public float AdjustForGoal(float tdee, int userGoal, int weightGoalPace)
    {
        return NutritionEnergyRule.AdjustForGoal(tdee, userGoal, weightGoalPace);
    }

    /// <summary>The target moved by the cycle phase's adjustment.</summary>
    public float AdjustForPhase(float calories, int cyclePhase)
    {
        return NutritionEnergyRule.AdjustForPhase(calories, cyclePhase);
    }

    /// <summary>A target kept inside the safe range, so no combination of inputs can prescribe too little.</summary>
    public float Clamp(float value, float min = 1200f, float max = 4000f)
    {
        return NutritionEnergyRule.Clamp(value, min, max);
    }

    /// <summary>The macro split for a calorie target, which is what the plan then distributes across meals.</summary>
    public (float Protein, float Carbs, float Fats) CalculateMacros(float targetCalories, float weightKg)
    {
        return NutritionEnergyRule.CalculateMacros(targetCalories, weightKg);
    }

    /// <summary>The multiplier for an activity level, with a middle value for an unknown one.</summary>
    public static float GetActivityMultiplier(int workoutDaysPerWeek)
    {
        return NutritionEnergyRule.GetActivityMultiplier(workoutDaysPerWeek);
    }
}
