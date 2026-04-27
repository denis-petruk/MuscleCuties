using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Services;

public interface INutritionService
{
    Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase);
    Task<float> GetConsumedCaloriesAsync(int userId, DateTime date);
    Task<(float Protein, float Carbs, float Fats)> GetConsumedMacrosAsync(int userId, DateTime date);
}
