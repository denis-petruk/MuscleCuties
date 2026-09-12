using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Cycle;

public interface ISymptomRepository : IRepository<SymptomLog>
{
    Task<List<SymptomLog>> GetByDateAsync(int userId, DateTime date);
    Task<List<SymptomLog>> GetByCycleAsync(int userId, int cycleLogId);
}
