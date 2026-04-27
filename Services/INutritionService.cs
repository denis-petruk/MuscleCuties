using MuscleCuties.Models.Enums;

namespace MuscleCuties.Services;

public interface INutritionService
{
    Task<(float calories, float protein, float carbs, float fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase);
    Task<float> GetConsumedCaloriesAsync(int userId, DateTime date);
    Task<(float protein, float carbs, float fats)> GetConsumedMacrosAsync(int userId, DateTime date);
}