namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

public sealed record DailyCheckInStatusRequest(DateOnly Date, string? TimeZoneId = null);

public sealed record TodayReadinessCheckInDto(
    string Status,
    decimal? RawScore,
    string? Band,
    int? SleepQuality,
    int? Energy,
    int? Soreness,
    int? Stress);

public sealed record DailyCheckInStatusResponse(
    bool IsCompleted,
    string ScoreText,
    string TierText,
    TodayReadinessCheckInDto? TodayReadiness = null);

public sealed record SubmitDailyCheckInRequest(
    DateOnly Date,
    double SleepHours,
    int EnergyLevel,
    int PainLevel,
    bool HasBloating,
    double? BodyWeightKg,
    int? PlanningSleepQuality = null,
    int? PlanningSoreness = null,
    int? PlanningStress = null,
    string? PlanningTimeZoneId = null);

public sealed record SubmitDailyCheckInResponse(
    int ReadinessScore,
    string ReadinessTier,
    string SessionSummary,
    bool PlanRefreshFailed,
    TodayReadinessCheckInDto? TodayReadiness = null);

public interface IDailyCheckInStore
{
    Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct);
    Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct);
}

public interface IDailyCheckInUseCase
{
    Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct = default);
    Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct = default);
}
