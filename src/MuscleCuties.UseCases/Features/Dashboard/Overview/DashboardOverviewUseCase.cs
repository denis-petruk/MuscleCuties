using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.Overview;

public sealed class DashboardOverviewUseCase(IDashboardOverviewStore store) : IDashboardOverviewUseCase
{
    public Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.LoadAsync(request, ct);
    }

    public Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!DashboardCalendarRule.IsValidMonth(request.Year, request.Month))
            throw new ArgumentOutOfRangeException(nameof(request), "Calendar month is invalid.");
        return store.LoadCalendarAsync(request, ct);
    }
}
