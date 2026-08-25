namespace MuscleCuties.Core.Services.Nutrition;

public interface ICalorieCalculator
{
    // Mifflin-St Jeor equation (women): 10W + 6.25H - 5A - 161
    float CalculateBmr(float weightKg, float heightCm, int age);
    float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek);
    float AdjustForGoal(float tdee, int userGoal, int weightGoalPace);
    float AdjustForPhase(float calories, int cyclePhase);
    float Clamp(float value, float min = 1200f, float max = 4000f);
    (float Protein, float Carbs, float Fats) CalculateMacros(float targetCalories, float weightKg);
}
