namespace MuscleCuties.Core.Domain.Dashboard;

public sealed record DashboardTrendObservation(
    DateOnly Date,
    double? WeightKg,
    int? ReadinessScore);

public sealed record DashboardTrendResultPoint(
    DateOnly Date,
    double? SmoothedWeightKg,
    int? ReadinessScore);

public static class DashboardTrendRule
{
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
