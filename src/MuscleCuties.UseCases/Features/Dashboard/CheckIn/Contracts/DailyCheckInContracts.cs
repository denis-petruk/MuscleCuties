namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

/// <summary>A request for whether today's check-in is done.</summary>
public sealed record DailyCheckInStatusRequest(DateOnly Date, string? TimeZoneId = null);

/// <summary>Today's readiness answers as the check-in form shows them.</summary>
public sealed record TodayReadinessCheckInDto(
    string Status,
    decimal? RawScore,
    string? Band,
    int? SleepQuality,
    int? Energy,
    int? Soreness,
    int? Stress,
    int? Fatigue = null);

/// <summary>Whether today's check-in is present, and what it said.</summary>
public sealed record DailyCheckInStatusResponse(
    bool IsCompleted,
    string ScoreText,
    string TierText,
    TodayReadinessCheckInDto? TodayReadiness = null);

/// <summary>How the check-in form should present today, which depends on what is already known.</summary>
public sealed record DailyCheckInPresentationSettings(
    bool IsReadinessScoreDisplayEnabled,
    bool AreReadinessSuggestionsEnabled);

/// <summary>A day's answers sent up, idempotent on its key.</summary>
public sealed record SubmitDailyCheckInRequest(
    DateOnly Date,
    double? SleepHours,
    int EnergyLevel,
    int? PainLevel,
    bool? HasBloating,
    double? BodyWeightKg,
    int? PlanningSleepQuality = null,
    int? PlanningSoreness = null,
    int? PlanningStress = null,
    string? PlanningTimeZoneId = null,
    int? PlanningFatigue = null);

/// <summary>What the server derived from the answers, so the client can show the effect immediately.</summary>
public sealed record SubmitDailyCheckInResponse(
    int ReadinessScore,
    string ReadinessTier,
    string SessionSummary,
    bool PlanRefreshFailed,
    TodayReadinessCheckInDto? TodayReadiness = null);

/// <summary>The persistence the check-in needs.</summary>
public interface IDailyCheckInStore
{
    Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct);
    Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct);
}

/// <summary>Reads the check-in status and saves an answer set.</summary>
public interface IDailyCheckInUseCase
{
    DailyCheckInPresentationSettings PresentationSettings { get; }
    Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct = default);
    Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct = default);
}
