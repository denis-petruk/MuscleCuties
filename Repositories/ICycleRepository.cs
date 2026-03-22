using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface ICycleRepository : IRepository<CycleLog>
{
    Task<CycleLog?> GetLatestCycleAsync(int userId);
    Task<List<CycleLog>> GetCycleHistoryAsync(int userId);
}