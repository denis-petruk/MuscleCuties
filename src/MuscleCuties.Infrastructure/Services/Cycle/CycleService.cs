using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.Services.Cycle;

public class CycleService : ICycleService
{
    private readonly ICycleRepository _cycleRepository;
    private readonly ICyclePredictionPlanner _predictionPlanner;
    private readonly IUserRepository _userRepository;

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

    public async Task<CyclePhase> GetPhaseForDateAsync(int userId, DateTime date)
    {
        var target = date.Date;
        var history = await _cycleRepository.GetCycleHistoryAsync(userId);
        var profile = await _userRepository.GetProfileAsync(userId);
        var cycle = history
            .Where(item => item.StartDate.Date <= target)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        var phaseLog = await _cycleRepository.GetLatestPhaseLogOnOrBeforeAsync(userId, target);
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(
            cycle?.CycleLength is > 0 ? cycle.CycleLength : profile?.CycleLength ?? 0);

        if (phaseLog is not null && (cycle is null || phaseLog.LoggedAt.Date >= cycle.StartDate.Date) &&
            (target - phaseLog.LoggedAt.Date).Days <= cycleLength * 3)
            return phaseLog.LoggedAt.Date == target
                ? phaseLog.Phase
                : CyclePhaseRules.ProjectPhaseFromLog(
                    new CyclePhaseLogProjection(phaseLog.Phase, phaseLog.LoggedAt), target, cycleLength);

        if (cycle is not null && (target - cycle.StartDate.Date).Days <= cycleLength * 3)
        {
            var days = (target - cycle.StartDate.Date).Days;
            return CyclePhaseRules.CalculatePhase(days % cycleLength + 1, cycleLength);
        }

        // A current profile phase cannot be projected backwards without an anchor date.
        return target == DateTime.Today && profile?.CurrentCyclePhase is { } current
            ? current
            : CyclePhase.Unknown;
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
        if (latestPhaseLog is not null &&
            (today - latestPhaseLog.LoggedAt.Date).Days <=
            CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength) * 3)
            return WithManualPhaseProjection(prediction, latestPhaseLog, today);

        return prediction;
    }

    public async Task<IReadOnlyList<CycleLog>> GetCycleHistoryAsync(int userId)
    {
        return await _cycleRepository.GetCycleHistoryAsync(userId);
    }

    public async Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId)
    {
        return await _cycleRepository.GetLatestPhaseLogAsync(userId);
    }

    public async Task<IReadOnlyList<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count)
    {
        return await _cycleRepository.GetRecentPhaseLogsAsync(userId, count);
    }

    public async Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsThroughDateAsync(int userId, DateTime date)
    {
        return await _cycleRepository.GetPhaseLogsThroughDateAsync(userId, date);
    }

    public async Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsByDateRangeAsync(int userId, DateTime startDate,
        DateTime endDate)
    {
        return await _cycleRepository.GetPhaseLogsByDateRangeAsync(userId, startDate, endDate);
    }

    public async Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsForCalendarRangeAsync(int userId, DateTime startDate,
        DateTime endDate)
    {
        var rangeLogs = await _cycleRepository.GetPhaseLogsByDateRangeAsync(userId, startDate, endDate);
        var anchorLog = await _cycleRepository.GetLatestPhaseLogOnOrBeforeAsync(userId, startDate);

        if (anchorLog is null)
            return rangeLogs;

        if (rangeLogs.Count == 0 || !rangeLogs.Any(l =>
                (l.Id != 0 && l.Id == anchorLog.Id) ||
                (l.LoggedAt.Date == anchorLog.LoggedAt.Date && l.Phase == anchorLog.Phase)))
        {
            var result = new List<CyclePhaseLog>(rangeLogs.Count + 1) { anchorLog };
            result.AddRange(rangeLogs);
            return result;
        }

        return rangeLogs;
    }

    public async Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime date)
    {
        return await _cycleRepository.GetPhaseLogForDateAsync(userId, date);
    }

    public async Task<IReadOnlyList<SymptomLog>> GetSymptomsForDateAsync(int userId, DateTime date)
    {
        return await _cycleRepository.GetSymptomsForDateAsync(userId, date);
    }

    public async Task<IReadOnlyList<SymptomLog>> GetSymptomsByDateRangeAsync(int userId, DateTime startDate,
        DateTime endDate)
    {
        return await _cycleRepository.GetSymptomsByDateRangeAsync(userId, startDate, endDate);
    }

    public async Task SaveDailyCycleLogAsync(
        int userId,
        DateTime date,
        bool? isPeriodStart,
        bool? isPeriodEnd,
        FlowIntensity? flowIntensity,
        IReadOnlyList<SymptomType> symptoms,
        CyclePhase? phaseOverride = null,
        string? note = null)
    {
        var targetDate = date.Date;
        var input = new CycleQuickLogInput(targetDate, isPeriodStart, isPeriodEnd,
            flowIntensity, symptoms, phaseOverride, note);
        CycleQuickLogRule.Validate(input);
        var inferredPhase = CycleQuickLogRule.NeedsInferredPhase(input)
            ? await GetPhaseForDateAsync(userId, targetDate)
            : CyclePhase.Unknown;
        var decision = CycleQuickLogRule.Resolve(input, inferredPhase);
        var now = DateTime.UtcNow;
        if (decision.WritePhaseLog)
            await SavePhaseLogAsync(userId, decision.Phase, targetDate, decision.Note, true, flowIntensity);

        var cycle = await GetOrCreateCycleForPhaseLogAsync(userId, decision.Phase, targetDate);

        await _cycleRepository.DeleteSymptomsForDateAsync(userId, targetDate);
        if (symptoms.Count > 0)
        {
            var symptomLogs = symptoms.Distinct().Select(st => new SymptomLog
            {
                UserId = userId,
                CycleLogId = cycle.Id,
                Date = targetDate,
                SymptomType = st,
                Severity = 3,
                Notes = note,
                CreatedAt = now
            }).ToList();

            await _cycleRepository.AddSymptomLogsAsync(symptomLogs);
        }
    }

    public async Task SetPhaseForDateAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note)
    {
        await SavePhaseLogAsync(userId, phase, loggedAt, note, true);
    }

    private async Task SavePhaseLogAsync(
        int userId,
        CyclePhase phase,
        DateTime loggedAt,
        string? note,
        bool updateExistingDate,
        FlowIntensity? flowIntensity = null)
    {
        var loggedDate = loggedAt.Date;
        ValidateLogDate(loggedDate);
        if (phase == CyclePhase.Unknown || !Enum.IsDefined(phase))
            throw new ArgumentException("Choose a known phase.", nameof(phase));
        await EnsurePhaseOrderAsync(userId, phase, loggedDate);
        await UpdateCurrentProfilePhaseAsync(userId, phase);

        var cycle = await GetOrCreateCycleForPhaseLogAsync(userId, phase, loggedDate);
        var existingLog = updateExistingDate
            ? await _cycleRepository.GetPhaseLogForDateAsync(userId, loggedDate)
            : null;

        if (existingLog is not null)
        {
            existingLog.CycleLogId = cycle.Id;
            existingLog.Phase = phase;
            if (flowIntensity.HasValue)
                existingLog.FlowIntensity = flowIntensity.Value;
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
            FlowIntensity = flowIntensity,
            LoggedAt = loggedDate,
            Note = NormalizeNote(note),
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task EnsurePhaseOrderAsync(int userId, CyclePhase phase, DateTime loggedDate)
    {
        var previousLog = await _cycleRepository.GetLatestPhaseLogBeforeDateAsync(userId, loggedDate);
        var nextLog = await _cycleRepository.GetNextPhaseLogAfterDateAsync(userId, loggedDate);
        var cycleLength = await GetCycleLengthForOrderAsync(userId);
        var previousDayPhase = await ResolvePhaseForOrderAsync(
            userId,
            loggedDate.AddDays(-1),
            previousLog,
            cycleLength);

        if (previousDayPhase is not null && !FollowsCycleOrder(previousDayPhase.Value, phase))
        {
            var expectedPhase = CyclePhaseRules.GetNextPhase(previousDayPhase.Value);
            throw new CyclePhaseOrderException(
                $"This phase would break the cycle order. Yesterday was {previousDayPhase.Value}, so log {expectedPhase} before {phase}.",
                expectedPhase);
        }

        if (nextLog is not null &&
            (nextLog.LoggedAt.Date - loggedDate).Days <= cycleLength * 3)
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
        CyclePhaseLog? previousLog,
        int cycleLength)
    {
        var history = await _cycleRepository.GetCycleHistoryAsync(userId);
        var cycle = history
            .Where(item => item.StartDate.Date <= date.Date &&
                           (item.EndDate is null || date.Date < item.EndDate.Value.Date))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();

        if (previousLog is not null &&
            (cycle is null || previousLog.LoggedAt.Date >= cycle.StartDate.Date) &&
            (date.Date - previousLog.LoggedAt.Date).Days <= cycleLength * 3)
            return CyclePhaseRules.ProjectPhaseFromLog(
                new CyclePhaseLogProjection(previousLog.Phase, previousLog.LoggedAt),
                date,
                cycleLength);

        if (cycle is null || (date.Date - cycle.StartDate.Date).Days > cycleLength * 3)
            return null;

        var daysFromCycleStart = (date.Date - cycle.StartDate.Date).Days;
        var normalizedOffset = (daysFromCycleStart % cycleLength + cycleLength) % cycleLength;
        return CyclePhaseRules.CalculatePhase(normalizedOffset + 1, cycleLength);
    }

    private static bool FollowsCycleOrder(CyclePhase from, CyclePhase to)
    {
        return to == from || to == CyclePhaseRules.GetNextPhase(from);
    }

    private async Task UpdateCurrentProfilePhaseAsync(int userId, CyclePhase phase)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile is null || profile.CurrentCyclePhase == phase)
            return;

        profile.CurrentCyclePhase = phase;
        profile.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateProfileAsync(profile);
    }

    private async Task<CycleLog> AlignCycleToPeriodStartAsync(int userId, DateTime periodStartDate)
    {
        var now = DateTime.UtcNow;
        var startDate = periodStartDate.Date;
        var history = await _cycleRepository.GetCycleHistoryAsync(userId);
        var existingCycle = history.FirstOrDefault(cycle => cycle.StartDate.Date == startDate);

        if (existingCycle is not null) return existingCycle;

        var previousCycle = history
            .Where(cycle => cycle.StartDate.Date < startDate)
            .OrderByDescending(cycle => cycle.StartDate)
            .FirstOrDefault();
        var nextCycle = history
            .Where(cycle => cycle.StartDate.Date > startDate)
            .OrderBy(cycle => cycle.StartDate)
            .FirstOrDefault();

        if (previousCycle is not null &&
            (previousCycle.EndDate is null || previousCycle.EndDate.Value.Date > startDate))
        {
            previousCycle.EndDate = startDate;
            previousCycle.CycleLength = (startDate - previousCycle.StartDate.Date).Days;
            await _cycleRepository.UpdateAsync(previousCycle);
        }

        return await CreateActiveCycleAsync(userId, startDate, now, nextCycle?.StartDate.Date);
    }

    private async Task<CycleLog> GetOrCreateCycleForPhaseLogAsync(int userId, CyclePhase phase, DateTime loggedDate)
    {
        if (phase is CyclePhase.Menstrual)
            return await AlignCycleToPeriodStartAsync(userId, loggedDate);

        var history = await _cycleRepository.GetCycleHistoryAsync(userId);
        var matchingCycle = history
            .Where(cycle => cycle.StartDate.Date <= loggedDate.Date &&
                            (cycle.EndDate is null || loggedDate.Date < cycle.EndDate.Value.Date))
            .OrderByDescending(cycle => cycle.StartDate)
            .FirstOrDefault();

        if (matchingCycle is not null)
            return matchingCycle;

        var prediction = await GetPredictionAsync(userId);
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var anchorDay = CyclePhaseRules.GetPhaseAnchorDay(phase, cycleLength);
        var inferredStartDate = loggedDate.Date.AddDays(-(anchorDay - 1));
        ValidateLogDate(inferredStartDate);
        var nextCycle = history
            .Where(cycle => cycle.StartDate.Date > loggedDate.Date)
            .OrderBy(cycle => cycle.StartDate)
            .FirstOrDefault();
        return await CreateActiveCycleAsync(userId, inferredStartDate, DateTime.UtcNow, nextCycle?.StartDate.Date);
    }

    private async Task<CycleLog> CreateActiveCycleAsync(int userId, DateTime startDate, DateTime createdAt,
        DateTime? endDate = null)
    {
        var cycle = new CycleLog
        {
            UserId = userId,
            StartDate = startDate.Date,
            EndDate = endDate,
            CycleLength = endDate is null ? 0 : (endDate.Value.Date - startDate.Date).Days,
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

    private static void ValidateLogDate(DateTime date)
    {
        if (date.Date < new DateTime(1970, 1, 1) || date.Date > new DateTime(2100, 12, 31))
            throw new ArgumentOutOfRangeException(nameof(date), "Cycle logs must use a supported calendar date.");
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
