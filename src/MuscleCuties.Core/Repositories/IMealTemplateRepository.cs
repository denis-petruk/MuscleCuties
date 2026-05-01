using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IMealTemplateRepository : IRepository<MealTemplate>
{
    Task<List<MealTemplate>> GetSystemTemplatesAsync();
    Task<List<MealTemplate>> GetUserTemplatesAsync(int userId);
    Task<MealTemplate?> GetTemplateWithEntriesAsync(int templateId);
}