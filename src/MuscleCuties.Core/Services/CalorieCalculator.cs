namespace MuscleCuties.Core.Services;

public class CalorieCalculator : ICalorieCalculator
{
    // Mifflin-St Jeor (women): 10W + 6.25H - 5A - 161
    public float CalculateBmr(float weightKg, float heightCm, int age)
        => 10f * weightKg + 6.25f * heightCm - 5f * age - 161f;

    public float ApplyActivityMultiplier(float bmr, int workoutDaysPerWeek)
        => bmr * workoutDaysPerWeek switch
        {
            <= 2 => 1.375f,
            <= 4 => 1.55f,
            _ => 1.725f
        };

    // userGoal: 0=FatLoss, 1=MuscleTone, 2=Strength, 3=MaintainHealth
    // weightGoalPace: 0=Steady (300 kcal delta), 1=Aggressive (500 kcal delta)
    public float AdjustForGoal(float tdee, int userGoal, int weightGoalPace)
    {
        var delta = weightGoalPace == 1 ? 500f : 300f;
        return userGoal switch
        {
            0 => tdee - delta,
            2 => tdee + delta,
            _ => tdee
        };
    }

    // cyclePhase: 0=Menstrual, 1=Follicular, 2=Ovulatory, 3=Luteal
    public float AdjustForPhase(float calories, int cyclePhase)
        => calories + cyclePhase switch
        {
            0 => -100f,
            2 => +50f,
            3 => +150f,
            _ => 0f
        };

    public float Clamp(float value, float min = 1200f, float max = 4000f)
        => MathF.Max(min, MathF.Min(max, value));

    public (float Protein, float Carbs, float Fats) CalculateMacros(float targetCalories, float weightKg)
    {
        var protein = weightKg * 1.8f;
        var fats = targetCalories * 0.25f / 9f;
        var carbs = (targetCalories - protein * 4f - fats * 9f) / 4f;
        return (MathF.Max(protein, 0f), MathF.Max(carbs, 0f), MathF.Max(fats, 0f));
    }
}
