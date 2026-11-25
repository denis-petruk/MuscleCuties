namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>The calorie maths as the planner and the pages see it.</summary>
public interface ICalorieCalculator
{
    float CalculateBmr(float weightKg, float heightCm, int age);
    float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek);
    float AdjustForGoal(float tdee, int userGoal, int weightGoalPace);
    float AdjustForPhase(float calories, int cyclePhase);
    float Clamp(float value, float min = 1200f, float max = 4000f);
    (float Protein, float Carbs, float Fats) CalculateMacros(float targetCalories, float weightKg);
}
