using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.UseCases.Features.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Infrastructure.Profile.HealthSync;

public sealed class LocalProfileHealthSyncStore(IServiceScopeFactory scopeFactory) : IProfileHealthSyncStore
{
    public async Task<ProfileHealthSyncDto> LoadAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userId = await scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>()
            .GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        var healthSync = scope.ServiceProvider.GetService<IHealthSyncService>();
        if (healthSync is not null)
        {
            var available = healthSync.IsAvailable && healthSync is not DisabledHealthSyncService;
            if (!available || userId <= 0)
                return Unavailable(available);

            var status = await healthSync.GetStatusAsync(userId).ConfigureAwait(false);
            var summary = await healthSync.GetCachedWeeklySummaryAsync(userId).ConfigureAwait(false);
            return MapFromHealthSync(status, summary, available);
        }

        var orchestrator = scope.ServiceProvider.GetRequiredService<IReadinessSyncOrchestrator>();
        return userId <= 0
            ? Unavailable(orchestrator.IsAppleHealthAvailable)
            : Map(await orchestrator.GetProfileHealthSyncSummaryAsync(userId, cancellationToken)
                .ConfigureAwait(false), orchestrator.IsAppleHealthAvailable);
    }

    public async Task<ProfileHealthConnectResult> ConnectAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var healthSync = scope.ServiceProvider.GetService<IHealthSyncService>();
        if (healthSync is not null)
        {
            var available = healthSync.IsAvailable && healthSync is not DisabledHealthSyncService;
            if (!available)
                return new ProfileHealthConnectResult(false, "Health import is unavailable in this release.",
                    Unavailable(false));

            var currentUserId = await scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>()
                .GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
            if (currentUserId <= 0)
                return new ProfileHealthConnectResult(false, "Active user session not found.", Unavailable(true));

            var syncResult = await healthSync.SyncAsync(currentUserId, HealthDataSource.AppleHealth, cancellationToken)
                .ConfigureAwait(false);
            var status = await healthSync.GetStatusAsync(currentUserId).ConfigureAwait(false);
            var cached = await healthSync.GetCachedWeeklySummaryAsync(currentUserId).ConfigureAwait(false);
            return new ProfileHealthConnectResult(
                syncResult.IsConnected,
                syncResult.Message,
                MapFromHealthSync(status, cached, available));
        }

        var orchestrator = scope.ServiceProvider.GetRequiredService<IReadinessSyncOrchestrator>();
        if (!orchestrator.IsAppleHealthAvailable)
            return new ProfileHealthConnectResult(false, "Health import is unavailable in this release.",
                Unavailable(false));

        var userId = await scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>()
            .GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
            return new ProfileHealthConnectResult(false, "Active user session not found.", Unavailable(true));

        var result = await orchestrator.SyncAsync(userId, HealthDataSource.AppleHealth, cancellationToken)
            .ConfigureAwait(false);
        var summary = await orchestrator.GetProfileHealthSyncSummaryAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return new ProfileHealthConnectResult(result.IsConnected, result.Message,
            Map(summary, orchestrator.IsAppleHealthAvailable));
    }

    public async Task<ProfileHealthDisconnectResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var currentUserId = await scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>()
            .GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (currentUserId <= 0)
            return new ProfileHealthDisconnectResult("Active user session not found.", Unavailable(false));

        var healthSync = scope.ServiceProvider.GetRequiredService<IHealthSyncService>();
        await healthSync.DisconnectAsync(currentUserId, cancellationToken).ConfigureAwait(false);
        var status = await healthSync.GetStatusAsync(currentUserId).ConfigureAwait(false);
        return new ProfileHealthDisconnectResult("Apple Health steps disconnected and saved step data removed.",
            MapFromHealthSync(status, null, healthSync.IsAvailable));
    }

    private static ProfileHealthSyncDto MapFromHealthSync(
        HealthSyncStatus status,
        HealthWeeklySummary? summary,
        bool available)
    {
        var lastSynced = status.LastSyncedAt is null
            ? "No sync yet"
            : $"Last sync {status.LastSyncedAt.Value.ToLocalTime():MMM d, h:mm tt}";

        var readinessScore = 0;
        if (status.IsConnected && summary is not null)
        {
            var healthMetrics = new DashboardHealthMetrics(
                summary.HasMovementData,
                summary.AverageSteps,
                summary.HasSleepData,
                summary.AverageSleepHours,
                summary.RestingHeartRate,
                summary.AverageActiveEnergyKcal);
            var support = DashboardSupportRule.Build(
                new DashboardCycleMetrics(false, false, null, null, 1, 28, 14),
                CyclePhase.Follicular,
                0.85f,
                65f,
                3,
                "Upcoming",
                healthMetrics);
            readinessScore = Math.Max(0, support.ReadinessScore);
        }

        return new ProfileHealthSyncDto(
            available,
            status.SummaryText,
            status.IsConnected ? "Refresh steps" : "Connect steps",
            lastSynced,
            readinessScore,
            ReadinessSyncOrchestrator.BuildReadinessExplanation(status, summary),
            status.IsConnected);
    }

    private static ProfileHealthSyncDto Map(ProfileHealthSyncSummary summary, bool available)
    {
        return new ProfileHealthSyncDto(
            available,
            summary.StatusText,
            summary.Status.IsConnected ? "Refresh steps" : "Connect steps",
            summary.LastSyncedText,
            Math.Max(0, summary.ReadinessScore),
            summary.ReadinessExplanationText,
            summary.Status.IsConnected);
    }

    private static ProfileHealthSyncDto Unavailable(bool available)
    {
        return new ProfileHealthSyncDto(
            available, "Not connected", "Connect steps", "No sync yet", 0,
            "Numerical guidance is unavailable pending qualified review.", false);
    }
}
