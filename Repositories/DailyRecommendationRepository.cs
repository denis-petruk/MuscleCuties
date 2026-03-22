using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;
using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public class DailyRecommendationRepository(AppDatabase db)
    : BaseRepository<DailyRecommendation>(db), IDailyRecommendationRepository
{
    public async Task<DailyRecommendation?> GetByDateAsync(int userId, DateTime date) =>
        await _db.DailyRecommendations
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Date.Date == date.Date);

    public async Task<List<DailyRecommendation>> GetRecentAsync(int userId, int days) =>
        await _db.DailyRecommendations
            .Where(r => r.UserId == userId && r.Date >= DateTime.UtcNow.AddDays(-days))
            .OrderByDescending(r => r.Date)
            .ToListAsync();
}