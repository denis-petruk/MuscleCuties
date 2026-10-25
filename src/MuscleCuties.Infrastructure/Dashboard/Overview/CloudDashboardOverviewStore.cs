using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Dashboard.Overview;

public sealed class CloudDashboardOverviewStore(
    MobileJourneyCloudGateway cloudGateway,
    LocalDashboardOverviewStore? localFallback = null,
    IHealthSyncService? healthSync = null,
    IServiceScopeFactory? scopeFactory = null) : IDashboardOverviewStore
{
    private const int DefaultWorkoutDaysPerWeek = 3;

    public async Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct)
    {
        var cloud = await cloudGateway.GetDashboardOverviewAsync(request.Today, request.TimeZoneId, ct).ConfigureAwait(false);
        if (cloud.Status == BackendApiStatus.Success && cloud.Data is not null)
            return await EnrichWithHealthKitIfNeededAsync(cloud.Data).ConfigureAwait(false);

        if (localFallback is not null)
            return await localFallback.LoadAsync(request, ct).ConfigureAwait(false);

        return CloudStoreResponse.RequireData(cloud, "dashboard overview");
    }

    public async Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct)
    {
        var cloud = await cloudGateway.GetDashboardCalendarAsync(request.Year, request.Month, request.Today, ct).ConfigureAwait(false);
        if (cloud.Status == BackendApiStatus.Success && cloud.Data is not null)
            return cloud.Data;

        if (localFallback is not null)
            return await localFallback.LoadCalendarAsync(request, ct).ConfigureAwait(false);

        return CloudStoreResponse.RequireData(cloud, "dashboard calendar");
    }

    private async Task<DashboardOverviewResponse> EnrichWithHealthKitIfNeededAsync(DashboardOverviewResponse response)
    {
        if (!response.NeedsDailyCheckIn || healthSync is null || scopeFactory is null)
            return response;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var authService = scope.ServiceProvider.GetService<IAuthService>();
            if (authService is null)
                return response;

            var userId = await authService.GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return response;

            var summary = await healthSync.GetCachedWeeklySummaryAsync(userId).ConfigureAwait(false);
            if (summary is null ||
                (!summary.HasMovementData && !summary.HasSleepData &&
                 summary.RestingHeartRate is not > 0 && summary.AverageActiveEnergyKcal is not > 0))
            {
                return response;
            }

            var phase = Enum.TryParse<CyclePhase>(response.CurrentPhase, true, out var parsedPhase)
                ? parsedPhase
                : CyclePhase.Unknown;
            var caloriesProgress = response.TargetCalories > 0f
                ? Math.Clamp(response.ConsumedCalories / response.TargetCalories, 0f, 1f)
                : 0f;
            var healthMetrics = new DashboardHealthMetrics(
                summary.HasMovementData,
                summary.AverageSteps,
                summary.HasSleepData,
                summary.AverageSleepHours,
                summary.RestingHeartRate,
                summary.AverageActiveEnergyKcal);
            var cycleMetrics = new DashboardCycleMetrics(
                response.HasActiveCycle,
                response.DaysUntilPeriod <= 0,
                null,
                null,
                response.CurrentCycleDay,
                response.PredictedCycleLength,
                response.DaysUntilPeriod);

            var support = DashboardSupportRule.Build(
                cycleMetrics,
                phase,
                caloriesProgress,
                weight: null,
                workoutDaysPerWeek: DefaultWorkoutDaysPerWeek,
                workoutSessionProgressText: response.SessionProgressText,
                health: healthMetrics,
                recordedReadinessScore: null,
                hasActiveInjury: response.HasActiveInjuries);

            return response with
            {
                ReadinessScore = support.ReadinessScore,
                ReadinessLabel = support.ReadinessLabel,
                RecoveryScore = support.RecoveryScore,
                RecoveryLabel = support.RecoveryLabel
            };
        }
        catch
        {
            return response;
        }
    }
}
