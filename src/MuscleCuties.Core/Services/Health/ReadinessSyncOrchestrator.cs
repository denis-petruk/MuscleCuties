using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.Services.Health;

public sealed class ReadinessSyncOrchestrator : IReadinessSyncOrchestrator
{
    private static readonly TimeSpan DefaultStalenessThreshold = TimeSpan.FromHours(3);

    private readonly IHealthSyncService _healthSyncService;
    private readonly IProfileAccountService _profileAccountService;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;
    private readonly IWorkoutService _workoutService;
    private readonly IDashboardPlanner _dashboardPlanner;

    public ReadinessSyncOrchestrator(
        IHealthSyncService healthSyncService,
        IProfileAccountService profileAccountService,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutService workoutService,
        IDashboardPlanner dashboardPlanner)
    {
        _healthSyncService = healthSyncService;
        _profileAccountService = profileAccountService;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        _workoutService = workoutService;
        _dashboardPlanner = dashboardPlanner;
    }

    public bool IsHealthSyncAvailable =>
        _healthSyncService is not DisabledHealthSyncService && _healthSyncService.IsAvailable;

    public bool IsAppleHealthAvailable =>
        OperatingSystem.IsIOS() && _healthSyncService is not DisabledHealthSyncService;

    public Task<HealthSyncResult> SyncAsync(
        int userId,
        HealthDataSource source,
        CancellationToken cancellationToken = default)
    {
        return _healthSyncService.SyncAsync(userId, source, cancellationToken);
    }

    public async Task<HealthWeeklySummary?> SyncIfStaleAndGetSummaryAsync(
        int userId,
        TimeSpan? maxStaleness = null,
        CancellationToken cancellationToken = default)
    {
        var stalenessThreshold = maxStaleness ?? DefaultStalenessThreshold;
        var healthStatus = await _healthSyncService.GetStatusAsync(userId);
        if (healthStatus.IsConnected &&
            healthStatus.SelectedSource is { } selectedSource &&
            (healthStatus.LastSyncedAt is null || healthStatus.LastSyncedAt < DateTime.UtcNow.Subtract(stalenessThreshold)))
        {
            await _healthSyncService.SyncAsync(userId, selectedSource, cancellationToken);
        }

        return await _healthSyncService.GetCachedWeeklySummaryAsync(userId);
    }

    public async Task<DashboardSupportSummary> ComputeDashboardSupportAsync(
        int userId,
        CyclePrediction prediction,
        CyclePhase phase,
        float caloriesProgress,
        float? weight,
        int workoutDaysPerWeek,
        TodaysWorkoutSummary workoutSummary,
        int? recordedReadinessScore = null,
        bool hasActiveInjury = false,
        int? localSteps = null,
        double? localAverageSleepHours = null,
        CancellationToken cancellationToken = default)
    {
        var healthSummary = await SyncIfStaleAndGetSummaryAsync(userId, cancellationToken: cancellationToken);

        return _dashboardPlanner.BuildSupportSummary(
            prediction,
            phase,
            caloriesProgress,
            weight,
            workoutDaysPerWeek,
            workoutSummary,
            healthSummary: healthSummary,
            recordedReadinessScore: recordedReadinessScore,
            hasActiveInjury: hasActiveInjury,
            localSteps: localSteps,
            localAverageSleepHours: localAverageSleepHours);
    }

    public async Task<ProfileHealthSyncSummary> GetProfileHealthSyncSummaryAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var status = await _healthSyncService.GetStatusAsync(userId);
        var healthSummary = await _healthSyncService.GetCachedWeeklySummaryAsync(userId);
        var profile = await _profileAccountService.GetProfileAsync(userId);
        var prediction = await _cycleService.GetPredictionAsync(userId);
        var phase = prediction.CurrentPhase;
        var targets = await _nutritionService.CalculateDailyTargetsAsync(userId, phase);
        var consumed = await _nutritionService.GetConsumedTotalsAsync(userId, DateTime.Today);
        var workoutSummary = await _workoutService.GetTodaysSummaryAsync(userId, phase, DateTime.Today);
        var supportSummary = _dashboardPlanner.BuildSupportSummary(
            prediction,
            phase,
            targets.Calories <= 0f ? 0f : Math.Clamp(consumed.Calories / targets.Calories, 0f, 1f),
            profile?.Weight,
            profile?.WorkoutDaysPerWeek ?? 0,
            workoutSummary,
            healthSummary);

        var statusText = status.SummaryText;
        var connectionBadgeText = status.IsConnected ? "Connected" : "Tap to Connect";
        var lastSyncedText = status.LastSyncedAt is null
            ? "No sync yet"
            : $"Last sync {status.LastSyncedAt.Value.ToLocalTime():MMM d, h:mm tt}";
        var explanationText = BuildReadinessExplanation(status, healthSummary);

        return new ProfileHealthSyncSummary(
            status,
            healthSummary,
            supportSummary.ReadinessScore,
            explanationText,
            statusText,
            connectionBadgeText,
            lastSyncedText);
    }

    public static string BuildReadinessExplanation(
        HealthSyncStatus status,
        HealthWeeklySummary? healthSummary)
    {
        if (!status.IsConnected)
            return "Numerical guidance is unavailable pending qualified review.";

        if (healthSummary is null)
            return "Connected, waiting for weekly steps and sleep.";

        if (healthSummary.HasMovementData && healthSummary.HasSleepData)
            return "Synced sleep, steps, and available heart and energy data shape today's plan.";

        if (healthSummary.HasMovementData)
            return "Synced steps are included; sleep is still estimated.";

        if (healthSummary.HasSleepData)
            return "Synced sleep is included; steps are still estimated.";

        return healthSummary.RestingHeartRate > 0 || healthSummary.AverageActiveEnergyKcal is > 0
            ? "Heart rate or active energy is included; sleep and steps are still estimated."
            : "Connected, but still estimating until weekly data arrives.";
    }
}
