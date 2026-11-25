namespace MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

/// <summary>A request for the dashboard as of a date.</summary>
public sealed record DashboardOverviewRequest(DateOnly Today, string? TimeZoneId = null);

/// <summary>A request for the calendar strip around a month.</summary>
public sealed record DashboardCalendarRequest(int Year, int Month, DateOnly Today);

/// <summary>One point on a trend line.</summary>
public sealed record DashboardTrendPointDto(DateOnly Date, double? WeightKg, int? ReadinessScore);

/// <summary>The week's nutrition summary shown on the dashboard.</summary>
public sealed record DashboardWeeklyNutritionDto(
    DateOnly WeekStart,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams);

/// <summary>The week's weight summary, with the change the trend implies.</summary>
public sealed record DashboardWeeklyWeightDto(DateOnly WeekStart, double WeightKg);

/// <summary>How many sessions were completed against the week's target.</summary>
public sealed record DashboardWorkoutProgressDto(
    double? HeaviestLiftKg,
    double? HighestSessionVolumeKg,
    int? LongestSessionMinutes,
    double? LongestDistanceKm,
    int? FastestPaceSecondsPerKm,
    string? HeaviestLiftExerciseName = null);

/// <summary>One upcoming or recent session in the dashboard list.</summary>
public sealed record DashboardWorkoutActivityDto(string Tag, string Title, int ExerciseCount);

/// <summary>One calendar day with the sessions scheduled on it.</summary>
public sealed record DashboardCalendarDayDto(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool HasWorkout,
    bool HasPlannedWorkout,
    bool HasMealLogged);

/// <summary>The calendar strip, with the month it covers.</summary>
public sealed record DashboardCalendarResponse(IReadOnlyList<DashboardCalendarDayDto> Days);

/// <summary>The whole dashboard payload, assembled so the client renders one consistent view.</summary>
public sealed record DashboardOverviewResponse(
    string DisplayName,
    string CurrentPhase,
    string PredictionSource,
    bool HasActiveCycle,
    DateOnly? CurrentCycleStartDate,
    int CurrentCycleDay,
    int PredictedCycleLength,
    int DaysUntilPeriod,
    int CycleCount,
    bool NeedsDailyCheckIn,
    bool HasActiveInjuries,
    float ConsumedCalories,
    float ConsumedCarbs,
    float ConsumedFats,
    float ConsumedProtein,
    float TargetCalories,
    float TargetCarbs,
    float TargetFats,
    float TargetProtein,
    int WorkoutStreakDays,
    int NutritionStreakDays,
    bool HasTodayPhoto,
    string CycleInsightText,
    string HydrationConsumed,
    string HydrationGoal,
    string SleepGoal,
    int ReadinessScore,
    string ReadinessLabel,
    int RecoveryScore,
    string RecoveryLabel,
    string WorkoutTitle,
    string WorkoutSubtitle,
    string WorkoutDurationText,
    string WorkoutExercisesCount,
    string WorkoutIntensity,
    string SessionProgressText,
    string WorkoutActivityTag,
    int WorkoutDayId,
    IReadOnlyList<DashboardWorkoutActivityDto> WorkoutActivitySections,
    IReadOnlyList<DashboardTrendPointDto> TrendPoints,
    DashboardWeeklyNutritionDto? WeeklyNutrition = null,
    IReadOnlyList<DashboardWeeklyWeightDto>? WeeklyWeights = null,
    DashboardWorkoutProgressDto? WorkoutProgress = null,
    MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts.TodayReadinessCheckInDto? TodayReadiness = null);

/// <summary>The persistence the dashboard needs.</summary>
public interface IDashboardOverviewStore
{
    Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct);
    Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct);
}

/// <summary>The dashboard read.</summary>
public interface IDashboardOverviewUseCase
{
    Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct = default);
    Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct = default);
}
