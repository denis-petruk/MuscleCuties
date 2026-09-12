using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Cycle;

public class SymptomRepository(AppDatabase db) : BaseRepository<SymptomLog>(db), ISymptomRepository
{
    public async Task<List<SymptomLog>> GetByDateAsync(int userId, DateTime date)
    {
        return await _db.SymptomLogs
            .Where(s => s.UserId == userId && s.Date.Date == date.Date)
            .ToListAsync();
    }

    public async Task<List<SymptomLog>> GetByCycleAsync(int userId, int cycleLogId)
    {
        return await _db.SymptomLogs
            .Where(s => s.UserId == userId && s.CycleLogId == cycleLogId)
            .OrderBy(s => s.Date)
            .ToListAsync();
    }
}
