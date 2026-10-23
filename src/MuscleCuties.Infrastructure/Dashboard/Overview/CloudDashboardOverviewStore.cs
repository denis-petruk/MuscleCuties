using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Dashboard.Overview;

public sealed class CloudDashboardOverviewStore(
    MobileJourneyCloudGateway cloudGateway,
    LocalDashboardOverviewStore? localFallback = null) : IDashboardOverviewStore
{
    public async Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct)
    {
        var cloud = await cloudGateway.GetDashboardOverviewAsync(request.Today, request.TimeZoneId, ct).ConfigureAwait(false);
        if (cloud.Status == BackendApiStatus.Success && cloud.Data is not null)
            return cloud.Data;

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
}
