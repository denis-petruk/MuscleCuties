#if DEBUG
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.UI.Services.Health;

/// <summary>
/// DEBUG-only simulator fixture with obviously synthetic values. Used only when the app is launched with
/// SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1 on a simulator. Never compiled into Release and never used on a device.
/// </summary>
public sealed class SyntheticHealthDataProvider : IHealthDataProvider, IHealthAuthorizationProvider
{
    public const string EnvironmentVariable = "MC_SYNTHETIC_HEALTH";

    public static bool IsRequested =>
        string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "1", StringComparison.Ordinal);

    public HealthDataSource Source => HealthDataSource.AppleHealth;
    public string DisplayName => "Synthetic test data";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        // Fixed, obviously fake values: 9,400 average steps, 8,100 yesterday, 6.2 h last night.
        var summary = new HealthWeeklySummary(
            Source: Source,
            WeekStart: today.Date.AddDays(-7),
            WeekEnd: today.Date,
            AverageSteps: 9400,
            AverageSleepHours: 6.8,
            SleepQualityScore: 3,
            RestingHeartRate: 58,
            HrvScore: 55,
            SyncedAt: DateTime.UtcNow,
            YesterdaySteps: 8100,
            LastNightSleepHours: 6.2,
            AverageActiveEnergyKcal: 420,
            AverageSleep3dHours: 6.6);
        return Task.FromResult<HealthWeeklySummary?>(summary);
    }
}
#endif
