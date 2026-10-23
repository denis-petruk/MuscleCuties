using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.Overview;

public interface IServerDashboardOverviewStore
{
    Task<DashboardOverviewResponse> LoadOverviewAsync(
        Guid userId,
        DateOnly today,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default);

    Task<DashboardCalendarResponse> LoadCalendarAsync(
        Guid userId,
        int year,
        int month,
        DateOnly today,
        CancellationToken cancellationToken = default);
}
