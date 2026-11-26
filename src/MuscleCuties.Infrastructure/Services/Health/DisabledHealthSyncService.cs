namespace MuscleCuties.Core.Services.Health;

/// <summary>The health sync used where the platform offers none: every call answers as if disconnected, so the UI needs no</summary>
/// platform checks.</summary>
public sealed class DisabledHealthSyncService : IHealthSyncService
{
    // Intentional V1 boundary: a native adapter needs owner authorization, permission design and a device review.
    /// <summary>Reports no source, because there is nothing to read from.</summary>
    public HealthDataSource DeviceHealthSource =>
        OperatingSystem.IsAndroid() ? HealthDataSource.HealthConnect : HealthDataSource.AppleHealth;

    /// <summary>The neutral name the settings row shows instead of a store's name.</summary>
    public string DeviceHealthDisplayName => DeviceHealthSource.ToDisplayName();
    /// <summary>Always false: this build cannot read device health data.</summary>
    public bool IsAvailable => false;

    /// <summary>Reports every metric as unavailable because this build has no health source.</summary>
    public Task<HealthSyncStatus> GetStatusAsync(int userId)
    {
        return Task.FromResult(new HealthSyncStatus(
            null,
            false,
            true,
            null,
            "Health import is unavailable in this release."));
    }

    /// <summary>Never prompts, because there is nothing to connect to.</summary>
    public Task<bool> ShouldShowPromptAsync(int userId)
    {
        return Task.FromResult(false);
    }

    /// <summary>A no-op, since nothing was ever offered.</summary>
    public Task DismissPromptAsync(int userId)
    {
        return Task.CompletedTask;
    }

    /// <summary>A no-op, since there is no connection to end.</summary>
    public Task DisconnectAsync(int userId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>Reports that no health data could be synced.</summary>
    public Task<HealthSyncResult> SyncAsync(
        int userId,
        HealthDataSource source,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthSyncResult(
            source,
            false,
            null,
            "Health import is unavailable in this release."));
    }

    /// <summary>No summary, because nothing was ever read.</summary>
    public Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId)
    {
        return Task.FromResult<HealthWeeklySummary?>(null);
    }
}
