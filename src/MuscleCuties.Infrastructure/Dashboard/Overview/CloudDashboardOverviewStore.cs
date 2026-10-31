using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Dashboard.Overview;

public sealed class CloudDashboardOverviewStore(
    MobileJourneyCloudGateway cloudGateway) : IDashboardOverviewStore
{
    public async Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct)
    {
        var cloud = await cloudGateway.GetDashboardOverviewAsync(request.Today, ct).ConfigureAwait(false);
        if (cloud.Status == BackendApiStatus.Success && cloud.Data is not null)
            return cloud.Data;

        return CloudStoreResponse.RequireData(cloud, "dashboard overview");
    }

    public async Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request, CancellationToken ct)
    {
        var cloud = await cloudGateway.GetDashboardCalendarAsync(request.Year, request.Month, request.Today, ct).ConfigureAwait(false);
        if (cloud.Status == BackendApiStatus.Success && cloud.Data is not null)
            return cloud.Data;

        return CloudStoreResponse.RequireData(cloud, "dashboard calendar");
    }
}
