namespace MuscleCuties.Core.Services.Health;

public interface IHealthDataProvider
{
    HealthDataSource Source { get; }
    string DisplayName { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default);
}
