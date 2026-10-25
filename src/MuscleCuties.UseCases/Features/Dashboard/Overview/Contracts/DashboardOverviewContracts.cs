namespace MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

public sealed record DashboardOverviewRequest(DateOnly Today, string? TimeZoneId = null);

public sealed record DashboardCalendarRequest(int Year, int Month, DateOnly Today);

public sealed record DashboardTrendPointDto(DateOnly Date, double? WeightKg, int? ReadinessScore);

public sealed record DashboardWeeklyNutritionDto(
    DateOnly WeekStart,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams);

public sealed record DashboardWeeklyWeightDto(DateOnly WeekStart, double WeightKg);

public sealed record DashboardWorkoutProgressDto(
    double? HeaviestLiftKg,
    double? HighestSessionVolumeKg,
    int? LongestSessionMinutes,
    double? LongestDistanceKm,
    int? FastestPaceSecondsPerKm,
    string? HeaviestLiftExerciseName = null);

public sealed record DashboardWorkoutActivityDto(string Tag, string Title, int ExerciseCount);

public sealed record DashboardCalendarDayDto(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool HasWorkout,
    bool HasPlannedWorkout,
    bool HasMealLogged);

public sealed record DashboardCalendarResponse(IReadOnlyList<DashboardCalendarDayDto> Days);

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

public interface IDashboardOverviewStore
{
    Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct);
    Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct);
}

public interface IDashboardOverviewUseCase
{
    Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct = default);
    Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct = default);
}
