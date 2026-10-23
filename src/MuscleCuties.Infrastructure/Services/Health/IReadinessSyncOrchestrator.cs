using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;

namespace MuscleCuties.Core.Services.Health;

public sealed record ProfileHealthSyncSummary(
    HealthSyncStatus Status,
    HealthWeeklySummary? HealthSummary,
    int ReadinessScore,
    string ReadinessExplanationText,
    string StatusText,
    string ConnectionBadgeText,
    string LastSyncedText);

public interface IReadinessSyncOrchestrator
{
    bool IsHealthSyncAvailable { get; }
    bool IsAppleHealthAvailable { get; }

    Task<HealthSyncResult> SyncAsync(
        int userId,
        HealthDataSource source,
        CancellationToken cancellationToken = default);

    Task<HealthWeeklySummary?> SyncIfStaleAndGetSummaryAsync(
        int userId,
        TimeSpan? maxStaleness = null,
        CancellationToken cancellationToken = default);

    Task<DashboardSupportSummary> ComputeDashboardSupportAsync(
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
        CancellationToken cancellationToken = default);

    Task<ProfileHealthSyncSummary> GetProfileHealthSyncSummaryAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
