namespace MuscleCuties.Core.Domain.Dashboard;

public sealed record DashboardNutritionObservation(
    DateOnly Date,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    bool IsDailySummary);

public sealed record DashboardWeeklyNutritionTotals(
    DateOnly WeekStart,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams);

public sealed record DashboardWeightObservation(DateOnly Date, double WeightKg);
public sealed record DashboardWeeklyWeight(DateOnly WeekStart, double WeightKg);

public sealed record DashboardWorkoutProgressObservation(
    bool IsRestDay,
    double? WeightKg,
    double? VolumeKg,
    int? DurationSeconds,
    double? DistanceKm,
    int? PaceSecondsPerKm);

public sealed record DashboardWorkoutProgress(
    double? HeaviestLiftKg,
    double? HighestSessionVolumeKg,
    int? LongestSessionMinutes,
    double? LongestDistanceKm,
    int? FastestPaceSecondsPerKm);

public static class DashboardTrendSummaryRule
{
    private const int DaysInWeek = 7;
    private const int MinutesDivisor = 60;
    private const double MinimumPlausibleWeightKg = 15d;
    private const double MaximumPlausibleWeightKg = 500d;

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + DaysInWeek - (int)DayOfWeek.Monday) % DaysInWeek;
        return date.AddDays(-daysSinceMonday);
    }

    public static DashboardWeeklyNutritionTotals? BuildWeeklyNutrition(
        IEnumerable<DashboardNutritionObservation>? observations,
        DateOnly today)
    {
        var weekStart = StartOfWeek(today);
        var days = (observations ?? [])
            .Where(item => item.Date >= weekStart && item.Date <= today)
            .GroupBy(item => item.Date)
            .Select(group => group.Any(item => item.IsDailySummary)
                ? group.Where(item => item.IsDailySummary).Take(1)
                : group.Where(item => !item.IsDailySummary))
            .SelectMany(items => items)
            .ToArray();

        if (days.Length == 0)
            return null;

        return new DashboardWeeklyNutritionTotals(
            weekStart,
            days.Sum(item => SafeNonNegative(item.Calories)),
            days.Sum(item => SafeNonNegative(item.ProteinGrams)),
            days.Sum(item => SafeNonNegative(item.CarbsGrams)),
            days.Sum(item => SafeNonNegative(item.FatGrams)));
    }

    public static IReadOnlyList<DashboardWeeklyWeight> BuildWeeklyWeights(
        IEnumerable<DashboardWeightObservation>? observations,
        DateOnly today)
    {
        return (observations ?? [])
            .Where(item => item.Date <= today &&
                           double.IsFinite(item.WeightKg) &&
                           item.WeightKg is >= MinimumPlausibleWeightKg and <= MaximumPlausibleWeightKg)
            .GroupBy(item => StartOfWeek(item.Date))
            .OrderBy(group => group.Key)
            .Select(group => new DashboardWeeklyWeight(group.Key,
                group.OrderBy(item => item.Date).Last().WeightKg))
            .ToArray();
    }

    public static DashboardWorkoutProgress BuildWorkoutProgress(
        IEnumerable<DashboardWorkoutProgressObservation>? observations)
    {
        var active = (observations ?? []).Where(item => !item.IsRestDay).ToArray();
        return new DashboardWorkoutProgress(
            MaxPositive(active.Select(item => item.WeightKg)),
            MaxPositive(active.Select(item => item.VolumeKg)),
            active.Where(item => item.DurationSeconds > 0)
                .Select(item => (int?)Math.Ceiling(item.DurationSeconds!.Value / (double)MinutesDivisor))
                .Max(),
            MaxPositive(active.Select(item => item.DistanceKm)),
            active.Where(item => item.PaceSecondsPerKm > 0)
                .Select(item => item.PaceSecondsPerKm)
                .Min());
    }

    private static double SafeNonNegative(double value) => double.IsFinite(value) && value > 0d ? value : 0d;

    private static double? MaxPositive(IEnumerable<double?> values) => values
        .Where(value => value is > 0d && double.IsFinite(value.Value))
        .Max();
}
