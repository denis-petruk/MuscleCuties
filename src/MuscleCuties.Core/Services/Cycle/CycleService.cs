using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.Services.Cycle;

public class CycleService : ICycleService
{
    private readonly ICycleRepository _cycleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICyclePredictionPlanner _predictionPlanner;

    public CycleService(
        ICycleRepository cycleRepository,
        IUserRepository userRepository,
        ICyclePredictionPlanner predictionPlanner)
    {
        _cycleRepository = cycleRepository;
        _userRepository = userRepository;
        _predictionPlanner = predictionPlanner;
    }

    public async Task<CyclePhase> GetCurrentPhaseAsync(int userId)
    {
        var prediction = await GetPredictionAsync(userId);
        return prediction.CurrentPhase;
    }

    public async Task<CyclePrediction> GetPredictionAsync(int userId)
    {
        var latestCycle = await _cycleRepository.GetLatestCycleAsync(userId);
        var history = await _cycleRepository.GetCycleHistoryAsync(userId);
        var profile = await _userRepository.GetProfileAsync(userId);
        var activeCycle = latestCycle?.EndDate is null ? latestCycle : null;
        var today = DateTime.Today;
        var prediction = _predictionPlanner.CreatePrediction(activeCycle, history, profile, today);

        var latestPhaseLog = await _cycleRepository.GetLatestPhaseLogOnOrBeforeAsync(userId, today);
        if (latestPhaseLog is not null)
            return WithManualPhaseProjection(prediction, latestPhaseLog, today);

        return prediction;
    }

    public async Task<CycleLog?> GetCurrentCycleAsync(int userId) =>
        await _cycleRepository.GetLatestCycleAsync(userId);

    public async Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId) =>
        await _cycleRepository.GetLatestPhaseLogAsync(userId);

    public async Task<IReadOnlyList<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count) =>
        await _cycleRepository.GetRecentPhaseLogsAsync(userId, count);

    public async Task LogPhaseShiftAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note)
    {
        await SavePhaseLogAsync(userId, phase, loggedAt, note, updateExistingDate: false);
    }

    public async Task SetPhaseForDateAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note)
    {
        await SavePhaseLogAsync(userId, phase, loggedAt, note, updateExistingDate: true);
    }

    private async Task SavePhaseLogAsync(
        int userId,
        CyclePhase phase,
        DateTime loggedAt,
        string? note,
        bool updateExistingDate)
    {
        var loggedDate = loggedAt.Date;
        await EnsurePhaseOrderAsync(userId, phase, loggedDate);
        await SwitchToManualTrackingAsync(userId, phase);

        var cycle = await GetOrCreateCycleForPhaseLogAsync(userId, phase, loggedDate);
        var existingLog = updateExistingDate
            ? await _cycleRepository.GetPhaseLogForDateAsync(userId, loggedDate)
            : null;

        if (existingLog is not null)
        {
            existingLog.CycleLogId = cycle.Id;
            existingLog.Phase = phase;
            existingLog.Note = NormalizeNote(note);
            existingLog.CreatedAt = DateTime.UtcNow;
            await _cycleRepository.UpdatePhaseLogAsync(existingLog);
            return;
        }

        await _cycleRepository.AddPhaseLogAsync(new CyclePhaseLog
        {
            UserId = userId,
            CycleLogId = cycle.Id,
            Phase = phase,
            LoggedAt = loggedDate,
            Note = NormalizeNote(note),
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task EnsurePhaseOrderAsync(int userId, CyclePhase phase, DateTime loggedDate)
    {
        var orderedLogs = (await _cycleRepository.GetRecentPhaseLogsAsync(userId, 1000))
            .Where(log => log.LoggedAt.Date != loggedDate.Date)
            .OrderBy(log => log.LoggedAt)
            .ThenBy(log => log.CreatedAt)
            .ToList();
        var cycleLength = await GetCycleLengthForOrderAsync(userId);
        var previousDayPhase = await ResolvePhaseForOrderAsync(
            userId,
            loggedDate.AddDays(-1),
            orderedLogs,
            cycleLength);
        var nextLog = orderedLogs.FirstOrDefault(log => log.LoggedAt.Date > loggedDate.Date);

        if (previousDayPhase is not null && !FollowsCycleOrder(previousDayPhase.Value, phase))
        {
            var expectedPhase = CyclePhaseRules.GetNextPhase(previousDayPhase.Value);
            throw new CyclePhaseOrderException(
                $"This phase would break the cycle order. Yesterday was {previousDayPhase.Value}, so log {expectedPhase} before {phase}.",
                expectedPhase);
        }

        if (nextLog is not null)
        {
            var phaseBeforeNextLog = CyclePhaseRules.ProjectPhaseFromLog(
                new CyclePhaseLogProjection(phase, loggedDate),
                nextLog.LoggedAt.Date.AddDays(-1),
                cycleLength);

            if (!FollowsCycleOrder(phaseBeforeNextLog, nextLog.Phase))
            {
                var expectedPhase = CyclePhaseRules.GetNextPhase(phaseBeforeNextLog);
                throw new CyclePhaseOrderException(
                    $"This phase would break the cycle order. Log {expectedPhase} before {nextLog.Phase}.",
                    expectedPhase);
            }
        }
    }

    private async Task<int> GetCycleLengthForOrderAsync(int userId)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile?.CycleLength is > 0)
            return CyclePhaseRules.NormalizeCycleLength(profile.CycleLength);

        var latestCycle = await _cycleRepository.GetLatestCycleAsync(userId);
        return CyclePhaseRules.NormalizeCycleLength(latestCycle?.CycleLength ?? CyclePhaseRules.DefaultCycleLength);
    }

    private async Task<CyclePhase?> ResolvePhaseForOrderAsync(
        int userId,
        DateTime date,
        IReadOnlyList<CyclePhaseLog> orderedLogs,
        int cycleLength)
    {
        var latestPhaseLog = orderedLogs.LastOrDefault(log => log.LoggedAt.Date <= date.Date);
        if (latestPhaseLog is not null)
        {
            return CyclePhaseRules.ProjectPhaseFromLog(
                new CyclePhaseLogProjection(latestPhaseLog.Phase, latestPhaseLog.LoggedAt),
                date,
                cycleLength);
        }

        var latestCycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (latestCycle is null || latestCycle.StartDate.Date > date.Date)
            return null;

        var daysFromCycleStart = (date.Date - latestCycle.StartDate.Date).Days;
        var normalizedOffset = ((daysFromCycleStart % cycleLength) + cycleLength) % cycleLength;
        return CyclePhaseRules.CalculatePhase(normalizedOffset + 1, cycleLength);
    }

    private static bool FollowsCycleOrder(CyclePhase from, CyclePhase to) =>
        to == from || to == CyclePhaseRules.GetNextPhase(from);

    private static CyclePhase GetPreviousPhase(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => CyclePhase.Luteal,
        CyclePhase.Follicular => CyclePhase.Menstrual,
        CyclePhase.Ovulatory => CyclePhase.Follicular,
        CyclePhase.Luteal => CyclePhase.Ovulatory,
        _ => CyclePhase.Menstrual
    };

    private async Task SwitchToManualTrackingAsync(int userId, CyclePhase phase)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile is null)
            return;

        var hasChanges = false;
        if (profile.CycleTrackingMode is not CycleTrackingMode.ManualPhaseLogging)
        {
            profile.CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging;
            hasChanges = true;
        }

        if (profile.CurrentCyclePhase != phase)
        {
            profile.CurrentCyclePhase = phase;
            hasChanges = true;
        }

        if (!hasChanges)
            return;

        profile.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateProfileAsync(profile);
    }

    public async Task StartNewCycleAsync(int userId)
    {
        await AlignCycleToPeriodStartAsync(userId, DateTime.UtcNow.Date);
    }

    public async Task EndCurrentCycleAsync(int userId)
    {
        var cycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (cycle == null) return;

        cycle.EndDate = DateTime.UtcNow;
        cycle.CycleLength = Math.Max(1, (int)(cycle.EndDate.Value.Date - cycle.StartDate.Date).TotalDays);
        await _cycleRepository.UpdateAsync(cycle);
    }

    private async Task<CycleLog> AlignCycleToPeriodStartAsync(int userId, DateTime periodStartDate)
    {
        var now = DateTime.UtcNow;
        var startDate = periodStartDate.Date;
        var currentCycle = await _cycleRepository.GetLatestCycleAsync(userId);

        if (currentCycle is null)
            return await CreateActiveCycleAsync(userId, startDate, now);

        if (currentCycle.StartDate.Date == startDate)
        {
            if (currentCycle.EndDate is not null)
            {
                currentCycle.EndDate = null;
                currentCycle.CycleLength = 0;
                await _cycleRepository.UpdateAsync(currentCycle);
            }

            return currentCycle;
        }

        if (currentCycle.EndDate is null && currentCycle.StartDate.Date >= startDate)
        {
            currentCycle.StartDate = startDate;
            currentCycle.EndDate = null;
            currentCycle.CycleLength = 0;
            await _cycleRepository.UpdateAsync(currentCycle);
            return currentCycle;
        }

        if (currentCycle.EndDate is null)
        {
            currentCycle.EndDate = startDate;
            currentCycle.CycleLength = Math.Max(1, (startDate - currentCycle.StartDate.Date).Days);
            await _cycleRepository.UpdateAsync(currentCycle);
        }

        return await CreateActiveCycleAsync(userId, startDate, now);
    }

    private async Task<CycleLog> GetOrCreateCycleForPhaseLogAsync(int userId, CyclePhase phase, DateTime loggedDate)
    {
        if (phase is CyclePhase.Menstrual)
            return await AlignCycleToPeriodStartAsync(userId, loggedDate);

        var currentCycle = await _cycleRepository.GetLatestCycleAsync(userId);

        if (currentCycle is { EndDate: null })
            return currentCycle;

        var prediction = await GetPredictionAsync(userId);
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var anchorDay = CyclePhaseRules.GetPhaseAnchorDay(phase, cycleLength);
        var inferredStartDate = loggedDate.Date.AddDays(-(anchorDay - 1));
        return await CreateActiveCycleAsync(userId, inferredStartDate, DateTime.UtcNow);
    }

    private async Task<CycleLog> CreateActiveCycleAsync(int userId, DateTime startDate, DateTime createdAt)
    {
        var cycle = new CycleLog
        {
            UserId = userId,
            StartDate = startDate.Date,
            CycleLength = 0,
            CreatedAt = createdAt
        };

        await _cycleRepository.AddAsync(cycle);
        return cycle;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var trimmed = note.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }

    private static CyclePrediction WithManualPhaseProjection(
        CyclePrediction prediction,
        CyclePhaseLog latestPhaseLog,
        DateTime today)
    {
        var projectedPhase = CyclePhaseRules.ProjectPhaseFromLog(
            new CyclePhaseLogProjection(latestPhaseLog.Phase, latestPhaseLog.LoggedAt),
            today,
            prediction.PredictedCycleLength);

        return new CyclePrediction
        {
            HasActiveCycle = prediction.HasActiveCycle,
            CurrentCycleStartDate = prediction.CurrentCycleStartDate,
            CurrentDay = prediction.CurrentDay,
            PredictedCycleLength = prediction.PredictedCycleLength,
            CurrentPhase = projectedPhase,
            PredictedNextPeriodDate = prediction.PredictedNextPeriodDate,
            DaysUntilPeriod = prediction.DaysUntilPeriod,
            PredictedOvulationDate = prediction.PredictedOvulationDate,
            FertileWindowStartDate = prediction.FertileWindowStartDate,
            FertileWindowEndDate = prediction.FertileWindowEndDate,
            PredictionSource = "manual phase log"
        };
    }
}
