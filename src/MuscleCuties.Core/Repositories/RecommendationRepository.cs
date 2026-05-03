using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

[Obsolete("Not injected anywhere. Remove AddScoped from MauiProgram.cs line 56 first, then delete this class")]
public class RecommendationRepository(AppDatabase db) : BaseRepository<RecommendationSet>(db), IRecommendationRepository
{
    public async Task<RecommendationSet?> GetSetByDateAsync(int userId, DateTime date) =>
        await _db.RecommendationSets
            .Include(s => s.NutritionRecommendations)
            .Include(s => s.WorkoutRecommendations)
            .Include(s => s.WellnessRecommendations)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Date.Date == date.Date);

    public async Task AddNutritionRecommendationAsync(NutritionRecommendation rec)
    {
        await _db.NutritionRecommendations.AddAsync(rec);
        await _db.SaveChangesAsync();
    }

    public async Task AddWorkoutRecommendationAsync(WorkoutRecommendation rec)
    {
        await _db.WorkoutRecommendations.AddAsync(rec);
        await _db.SaveChangesAsync();
    }

    public async Task AddWellnessRecommendationAsync(WellnessRecommendation rec)
    {
        await _db.WellnessRecommendations.AddAsync(rec);
        await _db.SaveChangesAsync();
    }
}