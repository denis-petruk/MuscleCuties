using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class MealTemplateRepository(AppDatabase db) : BaseRepository<MealTemplate>(db), IMealTemplateRepository
{
    public async Task<List<MealTemplate>> GetSystemTemplatesAsync() =>
        await _db.MealTemplates.Where(t => t.IsSystem).ToListAsync();

    public async Task<List<MealTemplate>> GetUserTemplatesAsync(int userId) =>
        await _db.MealTemplates.Where(t => t.UserId == userId && !t.IsSystem).ToListAsync();

    public async Task<MealTemplate?> GetTemplateWithEntriesAsync(int templateId) =>
        await _db.MealTemplates
            .Include(t => t.Entries)
            .ThenInclude(e => e.FoodItem)
            .FirstOrDefaultAsync(t => t.Id == templateId);
}