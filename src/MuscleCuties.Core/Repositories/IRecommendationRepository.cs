using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

[Obsolete("Not injected anywhere. Remove AddScoped from MauiProgram.cs line 56 first, then delete this interface")]
public interface IRecommendationRepository : IRepository<RecommendationSet>
{
    Task<RecommendationSet?> GetSetByDateAsync(int userId, DateTime date);
    Task AddNutritionRecommendationAsync(NutritionRecommendation rec);
    Task AddWorkoutRecommendationAsync(WorkoutRecommendation rec);
    Task AddWellnessRecommendationAsync(WellnessRecommendation rec);
}