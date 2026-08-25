using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Models.Entities.Cycle;

namespace MuscleCuties.Core.Repositories.Cycle;

public interface ICycleRepository : IRepository<CycleLog>
{
    Task<CycleLog?> GetLatestCycleAsync(int userId);
    Task<List<CycleLog>> GetCycleHistoryAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogOnOrBeforeAsync(int userId, DateTime date);
    Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime loggedAt);
    Task<List<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count);
    Task AddPhaseLogAsync(CyclePhaseLog log);
    Task UpdatePhaseLogAsync(CyclePhaseLog log);
}
