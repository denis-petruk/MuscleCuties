using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Cycle;

public interface ICycleRepository : IRepository<CycleLog>
{
    Task<CycleLog?> GetLatestCycleAsync(int userId);
    Task<List<CycleLog>> GetCycleHistoryAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogOnOrBeforeAsync(int userId, DateTime date);
    Task<CyclePhaseLog?> GetLatestPhaseLogBeforeDateAsync(int userId, DateTime date);
    Task<CyclePhaseLog?> GetNextPhaseLogAfterDateAsync(int userId, DateTime date);
    Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime loggedAt);
    Task<List<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count);
    Task<List<CyclePhaseLog>> GetPhaseLogsThroughDateAsync(int userId, DateTime date);
    Task<List<CyclePhaseLog>> GetPhaseLogsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
    Task<List<SymptomLog>> GetSymptomsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
    Task AddPhaseLogAsync(CyclePhaseLog log);
    Task UpdatePhaseLogAsync(CyclePhaseLog log);
    Task<List<SymptomLog>> GetSymptomsForDateAsync(int userId, DateTime date);
    Task AddSymptomLogsAsync(IEnumerable<SymptomLog> symptomLogs);
    Task DeleteSymptomsForDateAsync(int userId, DateTime date);
}
