using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface IDailyRecommendationRepository : IRepository<DailyRecommendation>
{
    Task<DailyRecommendation?> GetByDateAsync(int userId, DateTime date);
    Task<List<DailyRecommendation>> GetRecentAsync(int userId, int days);
}