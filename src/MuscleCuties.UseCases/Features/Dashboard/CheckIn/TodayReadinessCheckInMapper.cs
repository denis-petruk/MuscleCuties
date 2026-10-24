using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

public static class TodayReadinessCheckInMapper
{
    public static TodayReadinessCheckInDto Map(
        int? sleepQuality,
        int? energy,
        int? soreness,
        int? stress,
        DateOnly date,
        string? recordedTimeZoneId,
        DateTimeOffset? recordedAtUtc,
        long revision,
        DateTimeOffset nowUtc,
        string? activeTimeZoneId)
    {
        var observation = energy.HasValue || sleepQuality.HasValue || soreness.HasValue || stress.HasValue ||
                          recordedTimeZoneId is not null || recordedAtUtc.HasValue
            ? new TodayCheckInObservation(date, recordedTimeZoneId ?? string.Empty,
                recordedAtUtc ?? DateTimeOffset.MinValue, revision,
                sleepQuality, energy, soreness, stress)
            : null;
        var result = TodayCheckInScoreRule.Evaluate(observation, nowUtc, activeTimeZoneId ?? string.Empty);
        return new TodayReadinessCheckInDto(result.Status.ToString(), result.RawScore, result.Band?.ToString(),
            sleepQuality, energy, soreness, stress);
    }
}
