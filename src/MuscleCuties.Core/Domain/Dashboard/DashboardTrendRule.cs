namespace MuscleCuties.Core.Domain.Dashboard;

/// <summary>One day's observation in a trend, which the rule smooths.</summary>
public sealed record DashboardTrendObservation(
    DateOnly Date,
    double? WeightKg,
    int? ReadinessScore);

/// <summary>One point of the trend line the client draws.</summary>
public sealed record DashboardTrendResultPoint(
    DateOnly Date,
    double? SmoothedWeightKg,
    int? ReadinessScore);

/// <summary>Smooths a series into a trend, ignoring days with no observation instead of plotting zeros.</summary>
public static class DashboardTrendRule
{
    /// <summary>Projects the observation history into dashboard trend points up to today.</summary>
    public static IReadOnlyList<DashboardTrendResultPoint> Build(
        IReadOnlyList<DashboardTrendObservation> history,
        DateOnly today)
    {
        var logs = (history ?? [])
            .Where(log => log.Date <= today)
            .GroupBy(log => log.Date)
            .ToDictionary(group => group.Key, group => group.Last());

        var points = new List<DashboardTrendResultPoint>(7);

        for (var offset = 6; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            var weights = Enumerable.Range(0, 7)
                .Select(daysBack => logs.GetValueOrDefault(day.AddDays(-daysBack))?.WeightKg)
                .Where(weight => weight is >= 15 and <= 500 && double.IsFinite(weight.Value))
                .Select(weight => weight!.Value)
                .OrderBy(weight => weight)
                .ToArray();

            double? smoothedWeight = weights.Length >= 3
                ? weights.Length % 2 == 0
                    ? (weights[weights.Length / 2 - 1] + weights[weights.Length / 2]) / 2d
                    : weights[weights.Length / 2]
                : null;

            var score = logs.GetValueOrDefault(day)?.ReadinessScore;

            points.Add(new DashboardTrendResultPoint(
                day,
                smoothedWeight,
                score is >= 0 and <= 100 ? score : null));
        }

        return points;
    }
}
