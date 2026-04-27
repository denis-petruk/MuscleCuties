using MuscleCuties.Models.Enums;
using MuscleCuties.Repositories;

namespace MuscleCuties.Services;

public class NutritionService : INutritionService
{
    private readonly IUserRepository _userRepository;
    private readonly INutritionRepository _nutritionRepository;

    public NutritionService(IUserRepository userRepository, INutritionRepository nutritionRepository)
    {
        _userRepository = userRepository;
        _nutritionRepository = nutritionRepository;
    }

    public async Task<(float calories, float protein, float carbs, float fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile == null) return (2000, 120, 200, 65);

        var bmr = 10 * profile.Weight + 6.25f * profile.Height - 5 * profile.Age - 161;

        var activityMultiplier = profile.WorkoutDaysPerWeek switch
        {
            <= 2 => 1.375f,
            <= 4 => 1.55f,
            _ => 1.725f
        };

        var tdee = bmr * activityMultiplier;

        var calories = profile.Goal switch
        {
            UserGoal.FatLoss => tdee - 400,
            UserGoal.Strength => tdee + 200,
            _ => tdee
        };

        calories += phase switch
        {
            CyclePhase.Menstrual => -100,
            CyclePhase.Luteal => +150,
            CyclePhase.Ovulatory => +50,
            _ => 0
        };

        var protein = profile.Weight * 1.8f;
        var fats = calories * 0.25f / 9;
        var carbs = (calories - protein * 4 - fats * 9) / 4;

        return (calories, protein, carbs, fats);
    }

    public async Task<float> GetConsumedCaloriesAsync(int userId, DateTime date)
    {
        var logs = await _nutritionRepository.GetFoodLogsByDateAsync(userId, date);
        return logs
            .Where(l => l.FoodItem != null)
            .Sum(l => l.FoodItem!.Calories * l.Grams / 100);
    }

    public async Task<(float protein, float carbs, float fats)> GetConsumedMacrosAsync(int userId, DateTime date)
    {
        var logs = await _nutritionRepository.GetFoodLogsByDateAsync(userId, date);
        var validLogs = logs.Where(l => l.FoodItem != null);

        return (
            validLogs.Sum(l => l.FoodItem!.Protein * l.Grams / 100),
            validLogs.Sum(l => l.FoodItem!.Carbs * l.Grams / 100),
            validLogs.Sum(l => l.FoodItem!.Fats * l.Grams / 100)
        );

    }
}
