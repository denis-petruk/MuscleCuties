using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

[Obsolete("Not injected anywhere. Remove AddScoped from MauiProgram.cs line 54 first, then delete this interface")]
public interface IMealTemplateRepository : IRepository<MealTemplate>
{
    Task<List<MealTemplate>> GetSystemTemplatesAsync();
    Task<List<MealTemplate>> GetUserTemplatesAsync(int userId);
    Task<MealTemplate?> GetTemplateWithEntriesAsync(int templateId);
}