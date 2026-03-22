using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface ISymptomRepository : IRepository<SymptomLog>
{
    Task<List<SymptomLog>> GetByDateAsync(int userId, DateTime date);
    Task<List<SymptomLog>> GetByCycleAsync(int userId, int cycleLogId);
}