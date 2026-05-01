using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IRecommendationRepository : IRepository<RecommendationSet>
{
    Task<RecommendationSet?> GetSetByDateAsync(int userId, DateTime date);
    Task AddNutritionRecommendationAsync(NutritionRecommendation rec);
    Task AddWorkoutRecommendationAsync(WorkoutRecommendation rec);
    Task AddWellnessRecommendationAsync(WellnessRecommendation rec);
}