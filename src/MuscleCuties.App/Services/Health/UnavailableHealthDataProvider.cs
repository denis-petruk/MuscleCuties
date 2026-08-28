using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.App.Services.Health;

public sealed class UnavailableHealthDataProvider : IHealthDataProvider
{
    public UnavailableHealthDataProvider(HealthDataSource source, string displayName)
    {
        Source = source;
        DisplayName = displayName;
    }

    public HealthDataSource Source { get; }
    public string DisplayName { get; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(
        DateTime today,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<HealthWeeklySummary?>(null);
}
