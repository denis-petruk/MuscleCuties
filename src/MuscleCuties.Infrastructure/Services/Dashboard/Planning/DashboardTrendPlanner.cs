using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.UI.Dashboard;

namespace MuscleCuties.Core.Services.Dashboard.Planning;

public static class DashboardTrendPlanner
{
    public static IReadOnlyList<DashboardTrendPoint> Build(
        IReadOnlyList<DailyReadinessLog> history,
        DateOnly today)
    {
        var observations = (history ?? [])
            .Select(h => new DashboardTrendObservation(h.Date, h.WeightKg, h.ReadinessScore))
            .ToList();

        var result = DashboardTrendRule.Build(observations, today);

        return result
            .Select(r => new DashboardTrendPoint(r.Date, r.SmoothedWeightKg, r.ReadinessScore))
            .ToList();
    }
}
