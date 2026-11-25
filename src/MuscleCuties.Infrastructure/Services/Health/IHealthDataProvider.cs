namespace MuscleCuties.Core.Services.Health;

/// <summary>Reads the device's health data for the app.</summary>
public interface IHealthDataProvider
{
    HealthDataSource Source { get; }
    string DisplayName { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default);
}

/// <summary>The providers that also ask for permission, kept separate so a provider without it still works.</summary>
public interface IHealthAuthorizationProvider
{
    Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default);
}
