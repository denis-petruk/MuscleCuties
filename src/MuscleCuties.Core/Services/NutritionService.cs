using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class NutritionService : INutritionService
{
    private readonly IUserRepository _userRepository;
    private readonly INutritionRepository _nutritionRepository;
    private readonly ICalorieCalculator _calorieCalculator;

    public NutritionService(
        IUserRepository userRepository,
        INutritionRepository nutritionRepository,
        ICalorieCalculator calorieCalculator)
    {
        _userRepository = userRepository;
        _nutritionRepository = nutritionRepository;
        _calorieCalculator = calorieCalculator;
    }

    public async Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile == null) return (2000f, 120f, 200f, 65f);

        var age = DateTime.UtcNow.Year - profile.DateOfBirth.Year;
        if (profile.DateOfBirth.Date > DateTime.UtcNow.AddYears(-age)) age--;

        var bmr = _calorieCalculator.CalculateBmr(profile.Weight, profile.Height, age);
        var tdee = _calorieCalculator.ApplyActivityMultiplier(bmr, profile.WorkoutDaysPerWeek);
        var adjusted = _calorieCalculator.AdjustForGoal(tdee, (int)profile.Goal, (int)profile.WeightGoalPace);
        var withPhase = _calorieCalculator.AdjustForPhase(adjusted, (int)phase);
        var calories = _calorieCalculator.Clamp(withPhase);

        var (protein, carbs, fats) = _calorieCalculator.CalculateMacros(calories, profile.Weight);
        return (calories, protein, carbs, fats);
    }

    public async Task<float> GetConsumedCaloriesAsync(int userId, DateTime date)
    {
        var logs = await _nutritionRepository.GetFoodLogsByDateAsync(userId, date);
        return logs
            .Where(l => l.FoodItem != null)
            .Sum(l => l.FoodItem!.Calories * l.Grams / 100f);
    }

    public async Task<(float Protein, float Carbs, float Fats)> GetConsumedMacrosAsync(int userId, DateTime date)
    {
        var logs = await _nutritionRepository.GetFoodLogsByDateAsync(userId, date);
        var valid = logs.Where(l => l.FoodItem != null).ToList();
        return (
            valid.Sum(l => l.FoodItem!.Protein * l.Grams / 100f),
            valid.Sum(l => l.FoodItem!.Carbs * l.Grams / 100f),
            valid.Sum(l => l.FoodItem!.Fats * l.Grams / 100f)
        );
    }
}
