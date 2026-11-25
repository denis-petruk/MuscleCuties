namespace MuscleCuties.Core.Domain.Dashboard;

/// <summary>One day's nutrition figures, as the weekly summary reads them.</summary>
public sealed record DashboardNutritionObservation(
    DateOnly Date,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    bool IsDailySummary);

/// <summary>The week's nutrition totals with the days they were averaged over.</summary>
public sealed record DashboardWeeklyNutritionTotals(
    DateOnly WeekStart,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams);

/// <summary>One recorded weight, which the weekly change is computed from.</summary>
public sealed record DashboardWeightObservation(DateOnly Date, double WeightKg);
/// <summary>The week's weight change, which needs two readings to exist at all.</summary>
public sealed record DashboardWeeklyWeight(DateOnly WeekStart, double WeightKg);

/// <summary>One completed session, counted toward the week's target.</summary>
public sealed record DashboardWorkoutProgressObservation(
    bool IsRestDay,
    double? WeightKg,
    double? VolumeKg,
    int? DurationSeconds,
    double? DistanceKm,
    int? PaceSecondsPerKm,
    string? ExerciseName = null);

/// <summary>The week's sessions against its target.</summary>
public sealed record DashboardWorkoutProgress(
    double? HeaviestLiftKg,
    double? HighestSessionVolumeKg,
    int? LongestSessionMinutes,
    double? LongestDistanceKm,
    int? FastestPaceSecondsPerKm,
    string? HeaviestLiftExerciseName = null);

/// <summary>Builds the weekly summaries, leaving a figure absent rather than inventing one.</summary>
public static class DashboardTrendSummaryRule
{
    private const int DaysInWeek = 7;
    private const int MinutesDivisor = 60;
    private const double MinimumPlausibleWeightKg = 15d;
    private const double MaximumPlausibleWeightKg = 500d;

    /// <summary>The Monday the week is measured from, so every summary covers the same days.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + DaysInWeek - (int)DayOfWeek.Monday) % DaysInWeek;
        return date.AddDays(-daysSinceMonday);
    }

    /// <summary>Averages the week's calorie and macro totals, or nothing when the week has no entries.</summary>
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

    /// <summary>Collects the week's weigh-ins in date order.</summary>
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

    /// <summary>Summarizes the week's training: sessions done, heaviest lift and rest days.</summary>
    public static DashboardWorkoutProgress BuildWorkoutProgress(
        IEnumerable<DashboardWorkoutProgressObservation>? observations)
    {
        var active = (observations ?? []).Where(item => !item.IsRestDay).ToArray();
        var heaviestLift = active
            .Where(item => item.WeightKg is > 0d && double.IsFinite(item.WeightKg.Value))
            .OrderByDescending(item => item.WeightKg)
            .ThenBy(item => string.IsNullOrWhiteSpace(item.ExerciseName))
            .ThenBy(item => item.ExerciseName, StringComparer.Ordinal)
            .FirstOrDefault();
        return new DashboardWorkoutProgress(
            heaviestLift?.WeightKg,
            MaxPositive(active.Select(item => item.VolumeKg)),
            active.Where(item => item.DurationSeconds > 0)
                .Select(item => (int?)Math.Ceiling(item.DurationSeconds!.Value / (double)MinutesDivisor))
                .Max(),
            MaxPositive(active.Select(item => item.DistanceKm)),
            active.Where(item => item.PaceSecondsPerKm > 0)
                .Select(item => item.PaceSecondsPerKm)
                .Min(),
            string.IsNullOrWhiteSpace(heaviestLift?.ExerciseName)
                ? null
                : heaviestLift.ExerciseName.Trim());
    }

    /// <summary>Returns the value when it is finite and positive, otherwise zero.</summary>
    private static double SafeNonNegative(double value) => double.IsFinite(value) && value > 0d ? value : 0d;

    /// <summary>The largest positive value in the sequence, or nothing when none is positive.</summary>
    private static double? MaxPositive(IEnumerable<double?> values) => values
        .Where(value => value is > 0d && double.IsFinite(value.Value))
        .Max();
}
