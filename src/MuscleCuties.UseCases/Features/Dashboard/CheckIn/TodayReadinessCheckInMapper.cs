using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

/// <summary>Maps stored readiness onto the form's DTO, filling the defaults the form expects.</summary>
public static class TodayReadinessCheckInMapper
{
    /// <summary>Maps a stored check-in onto today's readiness card.</summary>
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
        string? activeTimeZoneId,
        int? fatigue = null)
    {
        var observation = energy.HasValue || sleepQuality.HasValue || soreness.HasValue || stress.HasValue ||
                          fatigue.HasValue ||
                          recordedTimeZoneId is not null || recordedAtUtc.HasValue
            ? new TodayCheckInObservation(date, recordedTimeZoneId ?? string.Empty,
                recordedAtUtc ?? DateTimeOffset.MinValue, revision,
                sleepQuality, energy, soreness, stress)
            : null;
        var result = TodayCheckInScoreRule.Evaluate(observation, nowUtc, activeTimeZoneId ?? string.Empty);
        return new TodayReadinessCheckInDto(result.Status.ToString(), result.RawScore, result.Band?.ToString(),
            sleepQuality, energy, soreness, stress, fatigue);
    }
}
