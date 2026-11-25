using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.Overview;

/// <summary>Builds the dashboard on the server, where the reads are one query each.</summary>
public sealed class ServerDashboardOverviewUseCase(IServerDashboardOverviewStore store)
{
    private const int MinimumValidCalendarYear = 1970;
    private const int MaximumValidCalendarYear = 2099;
    private const int MinimumValidCalendarMonth = 1;
    private const int MaximumValidCalendarMonth = 12;

    /// <summary>Loads the dashboard overview for the calling user.</summary>
    public Task<DashboardOverviewResponse> LoadOverviewAsync(
        Guid userId,
        DateOnly today,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        return store.LoadOverviewAsync(userId, today, timeZoneId, cancellationToken);
    }

    /// <summary>Loads the dashboard calendar for the calling user and month.</summary>
    public Task<DashboardCalendarResponse> LoadCalendarAsync(
        Guid userId,
        int year,
        int month,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));

        if (year is < MinimumValidCalendarYear or > MaximumValidCalendarYear)
            throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between {MinimumValidCalendarYear} and {MaximumValidCalendarYear}.");

        if (month is < MinimumValidCalendarMonth or > MaximumValidCalendarMonth)
            throw new ArgumentOutOfRangeException(nameof(month), $"Month must be between {MinimumValidCalendarMonth} and {MaximumValidCalendarMonth}.");

        return store.LoadCalendarAsync(userId, year, month, today, cancellationToken);
    }
}
