// Weekly trend figures shown on the dashboard.
// Second half of DashboardViewModel; see its declaration for the full contract.

using System.Text;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UI.Services.Units;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Dashboard;

/// <summary>The dashboard's trend card: a week of nutrition, weight and workout progress, one view at a time, with every</summary>
/// figure converted to the saved unit system at the display seam only.</summary>
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

    /// <summary>True when the nutrition trend is shown.</summary>
    public bool IsNutritionTrendVisible => TrendViewIndex == NutritionTrendIndex;
    /// <summary>True when the weight trend is shown.</summary>
    public bool IsWeightTrendVisible => TrendViewIndex == WeightTrendIndex;
    /// <summary>True when the workout trend is shown.</summary>
    public bool IsWorkoutTrendVisible => TrendViewIndex == WorkoutTrendIndex;
    /// <summary>Which of the trend views is on screen, counted from one for the reader.</summary>
    public string TrendPositionText => $"{TrendViewIndex + 1} / {TrendViewCount}";

    // F06: every value below is formatted in the saved metric/imperial preference. Conversion
    // happens at the display seam only; the DTOs keep their canonical kg and km values.
    /// <summary>The weight unit of the saved system, so a converted figure is never read in the wrong unit.</summary>
    public string WeightUnitLabel => UnitsFormatter.WeightUnitLabel(_useMetricUnits);
    /// <summary>The distance unit of the saved system.</summary>
    public string DistanceUnitLabel => UnitsFormatter.DistanceUnitLabel(_useMetricUnits);
    /// <summary>The pace unit of the saved system.</summary>
    public string PaceUnitLabel => UnitsFormatter.PaceUnitLabel(_useMetricUnits);

    /// <summary>True when weekly nutrition totals are loaded.</summary>
    public bool HasWeeklyNutrition => _weeklyNutrition is not null;
    /// <summary>The week's calories in one line, empty until the week's nutrition has loaded.</summary>
    public string WeeklyCaloriesText => _weeklyNutrition is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Dashboard_Trend_WeeklyCaloriesFormat")),
            _weeklyNutrition.Calories.ToString("N0", CultureInfo.CurrentCulture));
    /// <summary>The week's protein in grams, with a dash when it is unknown.</summary>
    public string WeeklyProteinText => FormatGrams(_weeklyNutrition?.ProteinGrams);
    /// <summary>The week's carbohydrate in grams.</summary>
    public string WeeklyCarbsText => FormatGrams(_weeklyNutrition?.CarbsGrams);
    /// <summary>The week's fat in grams.</summary>
    public string WeeklyFatText => FormatGrams(_weeklyNutrition?.FatGrams);
    /// <summary>Protein's share of the week's macros, for the stacked bar.</summary>
    public double WeeklyProteinShare => MacroShare(_weeklyNutrition?.ProteinGrams);
    /// <summary>Carbohydrate's share of the week's macros.</summary>
    public double WeeklyCarbsShare => MacroShare(_weeklyNutrition?.CarbsGrams);
    /// <summary>Fat's share of the week's macros.</summary>
    public double WeeklyFatShare => MacroShare(_weeklyNutrition?.FatGrams);

    /// <summary>The weekly weight points drawn in the chart.</summary>
    public IReadOnlyList<DashboardTrendPoint> WeeklyWeightPoints { get; private set; } = [];
    /// <summary>True when the weight trend has points.</summary>
    public bool HasWeeklyWeights => WeeklyWeightPoints.Count > 0;
    /// <summary>The weight line: how many weigh-ins the week holds and the latest one in the saved unit, empty when there</summary>
    /// are none.</summary>
    public string WeeklyWeightSummary => WeeklyWeightPoints.Count == 0
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Dashboard_Trend_LoggedWeeksFormat")),
            WeeklyWeightPoints.Count,
            WeeklyWeightPoints[^1].WeightKg is { } latestKg
                ? UnitsFormatter.ToDisplayWeight(latestKg, _useMetricUnits).ToString("N1", CultureInfo.CurrentCulture)
                : "—",
            WeightUnitLabel);
    /// <summary>Each weigh-in as a day and a weight, joined on one line so the week can be read at a glance.</summary>
    public string WeeklyWeightValuesText => string.Join("  ·  ", WeeklyWeightPoints.Select(point =>
        $"{point.Date.ToString("MMM d", CultureInfo.CurrentCulture)}: {FormatWeight(point.WeightKg)}"));

    /// <summary>True only when at least one workout figure exists, so the view does not open onto empty tiles.</summary>
    public bool HasWorkoutProgress => _workoutProgress is not null &&
                                      (_workoutProgress.HeaviestLiftKg.HasValue ||
                                       _workoutProgress.HighestSessionVolumeKg.HasValue ||
                                       _workoutProgress.LongestSessionMinutes.HasValue ||
                                       _workoutProgress.LongestDistanceKm.HasValue ||
                                       _workoutProgress.FastestPaceSecondsPerKm.HasValue);
    /// <summary>The heaviest lift of the period in the saved unit.</summary>
    public string HeaviestLiftText => FormatWeight(_workoutProgress?.HeaviestLiftKg);
    /// <summary>The name of the heaviest lift's exercise.</summary>
    public string HeaviestLiftExerciseName => _workoutProgress?.HeaviestLiftExerciseName ?? string.Empty;
    /// <summary>True when a heaviest lift is named.</summary>
    public bool HasHeaviestLiftExerciseName => !string.IsNullOrWhiteSpace(HeaviestLiftExerciseName);
    /// <summary>The highest session volume, converted the same way.</summary>
    public string HighestVolumeText => FormatWeight(_workoutProgress?.HighestSessionVolumeKg);
    /// <summary>The longest session in minutes, with a dash when there is none.</summary>
    public string LongestSessionText => _workoutProgress?.LongestSessionMinutes is { } minutes
        ? DisplayFormat.Minutes(minutes)
        : "—";
    /// <summary>The longest distance covered, in the saved unit.</summary>
    public string LongestDistanceText => FormatDistance(_workoutProgress?.LongestDistanceKm);
    /// <summary>The fastest pace, in the saved unit.</summary>
    public string FastestPaceText => FormatPace(_workoutProgress?.FastestPaceSecondsPerKm);

    /// <summary>Shows the previous trend.</summary>
    [RelayCommand]
    private void PreviousTrend() => TrendViewIndex = (TrendViewIndex + TrendViewCount - 1) % TrendViewCount;

    /// <summary>Shows the next trend.</summary>
    [RelayCommand]
    private void NextTrend() => TrendViewIndex = (TrendViewIndex + 1) % TrendViewCount;

    /// <summary>Refreshes the trend flags when the shown trend changes.</summary>
    partial void OnTrendViewIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsNutritionTrendVisible));
        OnPropertyChanged(nameof(IsWeightTrendVisible));
        OnPropertyChanged(nameof(IsWorkoutTrendVisible));
        OnPropertyChanged(nameof(TrendPositionText));
    }

    /// <summary>Takes the overview's three trend figures into the card in one go, so all three views read the same</summary>
    /// payload.</summary>
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
                     nameof(HasWorkoutProgress), nameof(WeightUnitLabel), nameof(DistanceUnitLabel),
                     nameof(PaceUnitLabel),
                     nameof(HeaviestLiftText), nameof(HeaviestLiftExerciseName),
                     nameof(HasHeaviestLiftExerciseName), nameof(HighestVolumeText), nameof(LongestSessionText),
                     nameof(LongestDistanceText), nameof(FastestPaceText)
                 })
            OnPropertyChanged(property);
    }

    /// <summary>One macro's share of the week's grams across all three, which is what the stacked bar divides by.</summary>
    private double MacroShare(double? grams)
    {
        var total = (_weeklyNutrition?.ProteinGrams ?? 0d) +
                    (_weeklyNutrition?.CarbsGrams ?? 0d) +
                    (_weeklyNutrition?.FatGrams ?? 0d);
        return total > 0d && grams is > 0d ? Math.Clamp(grams.Value / total, 0d, 1d) : 0d;
    }

    /// <summary>Grams as the card shows them, with a dash when the value is unknown.</summary>
    private static string FormatGrams(double? grams) => grams is { } value
        ? DisplayFormat.Grams(value)
        : "—";

    /// <summary>A weight in the saved unit system, with a dash when it is unknown — the stored value stays in kilograms.</summary>
    private string FormatWeight(double? kilograms) => kilograms is { } value
        ? $"{UnitsFormatter.ToDisplayWeight(value, _useMetricUnits).ToString("N1", CultureInfo.CurrentCulture)} " +
          WeightUnitLabel
        : "—";

    /// <summary>A distance in the saved unit system, from the stored kilometres.</summary>
    private string FormatDistance(double? kilometres) => kilometres is { } value
        ? $"{UnitsFormatter.ToDisplayDistance(value, _useMetricUnits).ToString("N1", CultureInfo.CurrentCulture)} " +
          DistanceUnitLabel
        : "—";

    /// <summary>A pace in the saved unit system, from the stored seconds per kilometre.</summary>
    private string FormatPace(int? secondsPerKilometre)
    {
        if (secondsPerKilometre is not { } seconds)
            return "—";

        var displaySeconds = _useMetricUnits
            ? seconds
            : UnitsFormatter.SecondsPerKilometreToSecondsPerMile(seconds);
        var rounded = (int)Math.Round(displaySeconds, MidpointRounding.AwayFromZero);
        return $"{rounded / SecondsPerMinute}:{rounded % SecondsPerMinute:D2} {PaceUnitLabel}";
    }
}
