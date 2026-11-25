using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.Overview;

/// <summary>Assembles the dashboard from the profile, readiness, cycle and nutrition reads.</summary>
public sealed class DashboardOverviewUseCase(IDashboardOverviewStore store) : IDashboardOverviewUseCase
{
    /// <summary>The dashboard for that date, read once so its cards agree with each other.</summary>
    public Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.LoadAsync(request, ct);
    }

    /// <summary>Loads the dashboard calendar for the requested month.</summary>
    public Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!DashboardCalendarRule.IsValidMonth(request.Year, request.Month))
            throw new ArgumentOutOfRangeException(nameof(request), "Calendar month is invalid.");
        return store.LoadCalendarAsync(request, ct);
    }
}
