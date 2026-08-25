using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Repositories.Nutrition;

public interface IMealTemplateRepository : IRepository<MealTemplate>
{
    Task<List<MealTemplate>> GetSystemTemplatesAsync();
    Task<List<MealTemplate>> GetUserTemplatesAsync(int userId);
    Task<MealTemplate?> GetTemplateWithEntriesAsync(int templateId);
}
