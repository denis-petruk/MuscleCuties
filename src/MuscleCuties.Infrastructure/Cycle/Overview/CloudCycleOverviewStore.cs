using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Cycle.Overview;

namespace MuscleCuties.Infrastructure.Cycle.Overview;

public sealed class CloudCycleOverviewStore(
    IMuscleCutiesBackendApiClient apiClient,
    MobileJourneyCloudGateway cloudGateway) : ICycleOverviewStore
{
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";

    public async Task<CycleOverviewSnapshot?> LoadAsync(
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken)
    {
        var fromDate = DateOnly.FromDateTime(rangeStart);
        var throughDate = DateOnly.FromDateTime(rangeEnd);
        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        if (throughDate < fromDate)
            (fromDate, throughDate) = (throughDate, fromDate);

        var liveResponse = await apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={fromDate:yyyy-MM-dd}&through={throughDate:yyyy-MM-dd}&today={todayDate:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);

        if (liveResponse.Status == BackendApiStatus.Success && liveResponse.Data is not null)
            return MapServerOverview(liveResponse.Data);

        var cachedResult = await cloudGateway.GetCycleOverviewWithOfflineCacheAsync(todayDate, cancellationToken)
            .ConfigureAwait(false);
        if (cachedResult.Status is BackendApiStatus.Success or BackendApiStatus.OfflineStaleCacheHit &&
            cachedResult.Data is not null)
        {
            var phase = Enum.TryParse<CyclePhaseDto>(cachedResult.Data.CurrentPhase, true, out var cp) && Enum.IsDefined(cp)
                ? cp
                : CyclePhaseDto.Follicular;
            var startDate = cachedResult.Data.AsOfLocalDate.AddDays(-cachedResult.Data.DayOfCycle + 1)
                .ToDateTime(TimeOnly.MinValue);
            var nextPeriod = cachedResult.Data.PredictedNextPeriodStart?.ToDateTime(TimeOnly.MinValue);
            var ovulation = cachedResult.Data.AsOfLocalDate.AddDays(-cachedResult.Data.DayOfCycle + 14)
                .ToDateTime(TimeOnly.MinValue);
            var fertileStart = cachedResult.Data.AsOfLocalDate.AddDays(-cachedResult.Data.DayOfCycle + 10)
                .ToDateTime(TimeOnly.MinValue);
            var fertileEnd = cachedResult.Data.AsOfLocalDate.AddDays(-cachedResult.Data.DayOfCycle + 15)
                .ToDateTime(TimeOnly.MinValue);

            var summary = new CyclePredictionSummary(
                HasActiveCycle: cachedResult.Data.DayOfCycle > 0,
                CurrentCycleStartDate: startDate,
                CurrentDay: cachedResult.Data.DayOfCycle,
                PredictedCycleLength: cachedResult.Data.CycleLengthDays,
                CurrentPhase: phase,
                PredictedNextPeriodDate: nextPeriod,
                DaysUntilPeriod: Math.Max(0, cachedResult.Data.CycleLengthDays - cachedResult.Data.DayOfCycle),
                PredictedOvulationDate: ovulation,
                FertileWindowStartDate: fertileStart,
                FertileWindowEndDate: fertileEnd,
                PredictionSource: cachedResult.Data.PredictionSource);

            return new CycleOverviewSnapshot(summary, [], [], []);
        }


        return null;
    }

    private static CycleOverviewSnapshot MapServerOverview(ServerCycleOverview overview)
    {
        var prediction = overview.Prediction;
        var currentPhase = Enum.TryParse<CyclePhaseDto>(prediction.CurrentPhase, true, out var parsedPhase) &&
                           Enum.IsDefined(parsedPhase)
            ? parsedPhase
            : CyclePhaseDto.Unknown;

        var summary = new CyclePredictionSummary(
            HasActiveCycle: prediction.HasActiveCycle,
            CurrentCycleStartDate: prediction.CurrentCycleStartDate?.ToDateTime(TimeOnly.MinValue),
            CurrentDay: prediction.CurrentDay,
            PredictedCycleLength: prediction.PredictedCycleLength,
            CurrentPhase: currentPhase,
            PredictedNextPeriodDate: prediction.PredictedNextPeriodDate?.ToDateTime(TimeOnly.MinValue),
            DaysUntilPeriod: prediction.DaysUntilPeriod,
            PredictedOvulationDate: prediction.PredictedOvulationDate?.ToDateTime(TimeOnly.MinValue),
            FertileWindowStartDate: prediction.FertileWindowStartDate?.ToDateTime(TimeOnly.MinValue),
            FertileWindowEndDate: prediction.FertileWindowEndDate?.ToDateTime(TimeOnly.MinValue),
            PredictionSource: prediction.PredictionSource);

        var phaseLogs = overview.PhaseLogs
            .Select(log =>
            {
                var phase = Enum.TryParse<CyclePhaseDto>(log.PhaseValue, true, out var p) && Enum.IsDefined(p)
                    ? p
                    : CyclePhaseDto.Unknown;
                FlowIntensityDto? flow = !string.IsNullOrWhiteSpace(log.FlowIntensityValue) &&
                                      Enum.TryParse<FlowIntensityDto>(log.FlowIntensityValue, true, out var f) &&
                                      Enum.IsDefined(f)
                    ? f
                    : null;
                return new CyclePhaseEntry(
                    ToStableIntId(log.Id),
                    log.CycleId.HasValue ? ToStableIntId(log.CycleId.Value) : null,
                    phase,
                    flow,
                    log.LoggedAt,
                    log.Note,
                    log.CreatedAt);
            })
            .ToArray();

        var symptoms = overview.History.Symptoms
            .Select(symptom =>
            {
                var type = Enum.TryParse<SymptomTypeDto>(symptom.Type, true, out var st) && Enum.IsDefined(st)
                    ? st
                    : SymptomTypeDto.Other;
                return new CycleSymptomEntry(
                    ToStableIntId(symptom.Id),
                    ToStableIntId(symptom.CycleId),
                    symptom.Date.ToDateTime(TimeOnly.MinValue),
                    type,
                    symptom.Severity,
                    symptom.Notes);
            })
            .ToArray();

        var history = overview.History.Cycles
            .Select(cycle => new CycleHistoryItem(
                ToStableIntId(cycle.Id),
                cycle.StartDate.ToDateTime(TimeOnly.MinValue),
                cycle.EndDate?.ToDateTime(TimeOnly.MinValue),
                cycle.CycleLength))
            .ToArray();

        return new CycleOverviewSnapshot(summary, phaseLogs, symptoms, history);
    }

    private static int ToStableIntId(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var raw = BitConverter.ToInt32(bytes[..4]) & int.MaxValue;
        return raw == 0 ? 1 : raw;
    }
}
