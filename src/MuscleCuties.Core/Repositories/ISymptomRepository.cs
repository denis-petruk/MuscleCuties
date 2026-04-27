using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface ISymptomRepository : IRepository<SymptomLog>
{
    Task<List<SymptomLog>> GetByDateAsync(int userId, DateTime date);
    Task<List<SymptomLog>> GetByCycleAsync(int userId, int cycleLogId);
}
