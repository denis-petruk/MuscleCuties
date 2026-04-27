using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface ICycleRepository : IRepository<CycleLog>
{
    Task<CycleLog?> GetLatestCycleAsync(int userId);
    Task<List<CycleLog>> GetCycleHistoryAsync(int userId);
}
