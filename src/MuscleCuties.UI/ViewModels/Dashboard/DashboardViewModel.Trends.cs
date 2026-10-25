using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Dashboard;

public partial class DashboardViewModel
{
    private const int NutritionTrendIndex = 0;
    private const int WeightTrendIndex = 1;
    private const int WorkoutTrendIndex = 2;
    private const int TrendViewCount = 3;
    private const int SecondsPerMinute = 60;

    private DashboardWeeklyNutritionDto? _weeklyNutrition;
    private DashboardWorkoutProgressDto? _workoutProgress;

    [ObservableProperty] private int _trendViewIndex;

    public bool IsNutritionTrendVisible => TrendViewIndex == NutritionTrendIndex;
    public bool IsWeightTrendVisible => TrendViewIndex == WeightTrendIndex;
    public bool IsWorkoutTrendVisible => TrendViewIndex == WorkoutTrendIndex;
    public string TrendPositionText => $"{TrendViewIndex + 1} / {TrendViewCount}";

    public bool HasWeeklyNutrition => _weeklyNutrition is not null;
    public string WeeklyCaloriesText => _weeklyNutrition is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Dashboard_Trend_WeeklyCaloriesFormat")),
            _weeklyNutrition.Calories.ToString("N0", CultureInfo.CurrentCulture));
    public string WeeklyProteinText => FormatGrams(_weeklyNutrition?.ProteinGrams);
    public string WeeklyCarbsText => FormatGrams(_weeklyNutrition?.CarbsGrams);
    public string WeeklyFatText => FormatGrams(_weeklyNutrition?.FatGrams);
    public double WeeklyProteinShare => MacroShare(_weeklyNutrition?.ProteinGrams);
    public double WeeklyCarbsShare => MacroShare(_weeklyNutrition?.CarbsGrams);
    public double WeeklyFatShare => MacroShare(_weeklyNutrition?.FatGrams);

    public IReadOnlyList<DashboardTrendPoint> WeeklyWeightPoints { get; private set; } = [];
    public bool HasWeeklyWeights => WeeklyWeightPoints.Count > 0;
    public string WeeklyWeightSummary => WeeklyWeightPoints.Count == 0
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Dashboard_Trend_LoggedWeeksFormat")),
            WeeklyWeightPoints.Count,
            WeeklyWeightPoints[^1].WeightKg?.ToString("N1", CultureInfo.CurrentCulture));
    public string WeeklyWeightValuesText => string.Join("  ·  ", WeeklyWeightPoints.Select(point =>
        $"{point.Date.ToString("MMM d", CultureInfo.CurrentCulture)}: {point.WeightKg:N1} kg"));

    public bool HasWorkoutProgress => _workoutProgress is not null &&
                                      (_workoutProgress.HeaviestLiftKg.HasValue ||
                                       _workoutProgress.HighestSessionVolumeKg.HasValue ||
                                       _workoutProgress.LongestSessionMinutes.HasValue ||
                                       _workoutProgress.LongestDistanceKm.HasValue ||
                                       _workoutProgress.FastestPaceSecondsPerKm.HasValue);
    public string HeaviestLiftText => FormatMetric(_workoutProgress?.HeaviestLiftKg, "kg");
    public string HeaviestLiftExerciseName => _workoutProgress?.HeaviestLiftExerciseName ?? string.Empty;
    public bool HasHeaviestLiftExerciseName => !string.IsNullOrWhiteSpace(HeaviestLiftExerciseName);
    public string HighestVolumeText => FormatMetric(_workoutProgress?.HighestSessionVolumeKg, "kg");
    public string LongestSessionText => _workoutProgress?.LongestSessionMinutes is { } minutes
        ? $"{minutes} min"
        : "—";
    public string LongestDistanceText => FormatMetric(_workoutProgress?.LongestDistanceKm, "km");
    public string FastestPaceText => _workoutProgress?.FastestPaceSecondsPerKm is { } seconds
        ? $"{seconds / SecondsPerMinute}:{seconds % SecondsPerMinute:D2} /km"
        : "—";

    [RelayCommand]
    private void PreviousTrend() => TrendViewIndex = (TrendViewIndex + TrendViewCount - 1) % TrendViewCount;

    [RelayCommand]
    private void NextTrend() => TrendViewIndex = (TrendViewIndex + 1) % TrendViewCount;

    partial void OnTrendViewIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsNutritionTrendVisible));
        OnPropertyChanged(nameof(IsWeightTrendVisible));
        OnPropertyChanged(nameof(IsWorkoutTrendVisible));
        OnPropertyChanged(nameof(TrendPositionText));
    }

    private void ApplyTrendSummaries(DashboardOverviewResponse summary)
    {
        _weeklyNutrition = summary.WeeklyNutrition;
        _workoutProgress = summary.WorkoutProgress;
        WeeklyWeightPoints = (summary.WeeklyWeights ?? [])
            .OrderBy(point => point.WeekStart)
            .Select(point => new DashboardTrendPoint(point.WeekStart, point.WeightKg, null))
            .ToArray();

        foreach (var property in new[]
                 {
                     nameof(HasWeeklyNutrition), nameof(WeeklyCaloriesText), nameof(WeeklyProteinText),
                     nameof(WeeklyCarbsText), nameof(WeeklyFatText), nameof(WeeklyProteinShare),
                     nameof(WeeklyCarbsShare), nameof(WeeklyFatShare), nameof(WeeklyWeightPoints),
                     nameof(HasWeeklyWeights), nameof(WeeklyWeightSummary), nameof(WeeklyWeightValuesText),
                     nameof(HasWorkoutProgress),
                     nameof(HeaviestLiftText), nameof(HeaviestLiftExerciseName),
                     nameof(HasHeaviestLiftExerciseName), nameof(HighestVolumeText), nameof(LongestSessionText),
                     nameof(LongestDistanceText), nameof(FastestPaceText)
                 })
            OnPropertyChanged(property);
    }

    private double MacroShare(double? grams)
    {
        var total = (_weeklyNutrition?.ProteinGrams ?? 0d) +
                    (_weeklyNutrition?.CarbsGrams ?? 0d) +
                    (_weeklyNutrition?.FatGrams ?? 0d);
        return total > 0d && grams is > 0d ? Math.Clamp(grams.Value / total, 0d, 1d) : 0d;
    }

    private static string FormatGrams(double? grams) => grams is { } value
        ? $"{value.ToString("N0", CultureInfo.CurrentCulture)} g"
        : "—";

    private static string FormatMetric(double? value, string unit) => value is { } number
        ? $"{number.ToString("N1", CultureInfo.CurrentCulture)} {unit}"
        : "—";
}
