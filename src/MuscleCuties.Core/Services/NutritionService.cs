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
        var meals = await _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
        return meals
            .SelectMany(m => m.Entries)
            .Where(e => e.FoodItem != null)
            .Sum(e => e.FoodItem!.Calories * e.Grams / 100f);
    }

    public async Task<(float Protein, float Carbs, float Fats)> GetConsumedMacrosAsync(int userId, DateTime date)
    {
        var meals = await _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
        var entries = meals.SelectMany(m => m.Entries).Where(e => e.FoodItem != null).ToList();
        return (
            entries.Sum(e => e.FoodItem!.Protein * e.Grams / 100f),
            entries.Sum(e => e.FoodItem!.Carbs * e.Grams / 100f),
            entries.Sum(e => e.FoodItem!.Fats * e.Grams / 100f)
        );
    }
}