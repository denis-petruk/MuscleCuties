using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Repositories;

public class SymptomRepository(AppDatabase db) : BaseRepository<SymptomLog>(db), ISymptomRepository
{
    public async Task<List<SymptomLog>> GetByDateAsync(int userId, DateTime date) =>
        await _db.SymptomLogs
            .Where(s => s.UserId == userId && s.Date.Date == date.Date)
            .ToListAsync();

    public async Task<List<SymptomLog>> GetByCycleAsync(int userId, int cycleLogId) =>
        await _db.SymptomLogs
            .Where(s => s.UserId == userId && s.CycleLogId == cycleLogId)
            .OrderBy(s => s.Date)
            .ToListAsync();
}
