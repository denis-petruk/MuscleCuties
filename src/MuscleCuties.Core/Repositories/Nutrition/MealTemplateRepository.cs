using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Repositories.Nutrition;

public class MealTemplateRepository(AppDatabase db) : BaseRepository<MealTemplate>(db), IMealTemplateRepository
{
    public async Task<List<MealTemplate>> GetSystemTemplatesAsync() =>
        await _db.MealTemplates
            .AsNoTracking()
            .Include(t => t.Entries)
            .ThenInclude(e => e.FoodItem)
            .Where(t => t.IsSystem)
            .OrderBy(t => t.MealType)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();

    public async Task<List<MealTemplate>> GetUserTemplatesAsync(int userId) =>
        await _db.MealTemplates
            .AsNoTracking()
            .Include(t => t.Entries)
            .ThenInclude(e => e.FoodItem)
            .Where(t => t.UserId == userId && !t.IsSystem)
            .OrderBy(t => t.MealType)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();

    public async Task<MealTemplate?> GetTemplateWithEntriesAsync(int templateId) =>
        await _db.MealTemplates
            .AsNoTracking()
            .Include(t => t.Entries)
            .ThenInclude(e => e.FoodItem)
            .FirstOrDefaultAsync(t => t.Id == templateId);
}
