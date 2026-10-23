using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.Services.Cycle;

public interface ICycleService
{
    Task<CyclePhase> GetCurrentPhaseAsync(int userId);
    Task<CyclePhase> GetPhaseForDateAsync(int userId, DateTime date);
    Task<CyclePrediction> GetPredictionAsync(int userId);
    Task<IReadOnlyList<CycleLog>> GetCycleHistoryAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId);
    Task<IReadOnlyList<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count);
    Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsThroughDateAsync(int userId, DateTime date);
    Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);

    Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsForCalendarRangeAsync(int userId, DateTime startDate,
        DateTime endDate);

    Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime date);
    Task SetPhaseForDateAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note);
    Task<IReadOnlyList<SymptomLog>> GetSymptomsForDateAsync(int userId, DateTime date);
    Task<IReadOnlyList<SymptomLog>> GetSymptomsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);

    Task SaveDailyCycleLogAsync(
        int userId,
        DateTime date,
        bool? isPeriodStart,
        bool? isPeriodEnd,
        FlowIntensity? flowIntensity,
        IReadOnlyList<SymptomType> symptoms,
        CyclePhase? phaseOverride = null,
        string? note = null);
}
