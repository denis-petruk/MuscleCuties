using System.Collections.ObjectModel;
using System.Globalization;

namespace MuscleCuties.Core.Models.UI.Workout;

public class WorkoutExerciseItem
{
    private string _loggedEffortText = string.Empty;
    public bool IsExpanded { get; set; }
    public bool IsLogged { get; set; }
    public string LoggedSetsText { get; set; } = string.Empty;
    public string LoggedRepsText { get; set; } = string.Empty;
    public string LoggedWeightText { get; set; } = string.Empty;
    public string MovementNotes { get; set; } = string.Empty;
    public bool HasMovementNotes => !string.IsNullOrWhiteSpace(MovementNotes);

    public int WorkoutDayExerciseId { get; set; }
    public int ExerciseId { get; set; }
    public string ActivityTag { get; set; } = string.Empty;
    public string ActivityTitle { get; set; } = string.Empty;
    public string ActivityBackground { get; set; } = string.Empty;
    public string ActivityTextColor { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string TechniqueNotes { get; set; } = string.Empty;
    public string QuickTipsText { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public string PreviousText { get; set; } = string.Empty;
    public string RecommendationText { get; set; } = string.Empty;
    public bool UsesEnduranceMetrics { get; set; }
    public bool UsesStrengthMetrics => !UsesEnduranceMetrics;
    public bool UsesDurationMetric { get; set; }
    public bool UsesDistanceMetric { get; set; }
    public bool UsesPaceMetric { get; set; }
    public bool UsesHeartRateMetric { get; set; }
    public bool UsesPowerMetric { get; set; }
    public bool UsesCadenceMetric { get; set; }
    public bool UsesEffortMetric { get; set; }

    public bool UsesDurationOnlyMetric =>
        UsesDurationMetric &&
        !UsesDistanceMetric &&
        !UsesPaceMetric &&
        !UsesHeartRateMetric &&
        !UsesPowerMetric &&
        !UsesCadenceMetric &&
        !UsesEffortMetric;

    public bool UsesCardioMetricGrid =>
        UsesEnduranceMetrics &&
        !UsesDurationOnlyMetric;

    public bool UsesWeight { get; set; } = true;
    public string DurationLabel { get; set; } = "Minutes";
    public string DistanceLabel { get; set; } = "km";
    public string PaceLabel { get; set; } = "Pace / km";
    public string HeartRateLabel { get; set; } = "Avg bpm";
    public string PowerLabel { get; set; } = "Watts";
    public string CadenceLabel { get; set; } = "RPM";
    public string EffortLabel { get; set; } = "Effort";
    public ObservableCollection<ExerciseSetInputItem> Sets { get; } = [];
    public string LoggedDurationMinutesText { get; set; } = string.Empty;
    public string LoggedDistanceKmText { get; set; } = string.Empty;
    public string LoggedPaceText { get; set; } = string.Empty;
    public string LoggedHeartRateText { get; set; } = string.Empty;
    public string LoggedPowerWattsText { get; set; } = string.Empty;
    public string LoggedCadenceRpmText { get; set; } = string.Empty;

    public string LoggedEffortText
    {
        get => _loggedEffortText;
        set => _loggedEffortText = value ?? string.Empty;
    }

    public double EffortSliderValue
    {
        get => TryGetEffort(out var effort) ? effort : 5d;
        set => SetEffort(value, false);
    }

    public string EffortDisplayText => TryGetEffort(out var effort) ? $"{effort} / 10" : "Move to choose";

    public string DetailsButtonText => IsExpanded ? "Hide" : "Info";
    public string LogButtonText => IsLogged ? "Update" : "Log";
    public string LogStatusText => IsLogged ? "Logged today" : "Not logged yet";
    public bool HasVideo => !string.IsNullOrWhiteSpace(VideoUrl);
    public string VideoStatusText => HasVideo ? "Technique video saved" : string.Empty;

    public string WhyText => string.IsNullOrWhiteSpace(Description)
        ? "This movement supports today's workout focus and keeps the session balanced."
        : Description;

    public string CuesText => string.IsNullOrWhiteSpace(TechniqueNotes)
        ? Description
        : TechniqueNotes;

    public bool HasQuickTips => !string.IsNullOrWhiteSpace(QuickTipsText);

    public void AddSet()
    {
        var nextSetNumber = Sets.Count + 1;
        var previousSet = Sets.LastOrDefault();
        var weight = previousSet?.WeightKg ??
                     (double.TryParse(LoggedWeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                         ? w
                         : 0.0);
        var reps = previousSet?.RepsCompleted ??
                   (int.TryParse(LoggedRepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                       ? r
                       : 10);
        var rest = previousSet?.RestTimeSeconds ?? 90;

        var set = new ExerciseSetInputItem(
            Guid.NewGuid(),
            Guid.Empty,
            ExerciseId.ToString(CultureInfo.InvariantCulture),
            Name,
            nextSetNumber,
            weight,
            reps,
            null,
            false,
            rest)
        {
            OnChanged = UpdateAggregatesFromSets
        };
        Sets.Add(set);
        UpdateAggregatesFromSets();
    }

    public void RemoveSet(ExerciseSetInputItem? set)
    {
        if (set is null || Sets.Count <= 1) return;
        Sets.Remove(set);
        for (var i = 0; i < Sets.Count; i++) Sets[i].SetNumber = i + 1;
        UpdateAggregatesFromSets();
    }

    public void EnsureSets(int count = 3, Guid sessionId = default)
    {
        if (Sets.Count > 0) return;
        var targetCount = Math.Max(1, count);
        var defaultWeight =
            double.TryParse(LoggedWeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0.0;
        var defaultReps = int.TryParse(LoggedRepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) &&
                          r > 0
            ? r
            : 10;
        for (var i = 1; i <= targetCount; i++)
        {
            var set = new ExerciseSetInputItem(
                Guid.NewGuid(),
                sessionId,
                ExerciseId.ToString(CultureInfo.InvariantCulture),
                Name,
                i,
                defaultWeight,
                defaultReps,
                null,
                false,
                90)
            {
                OnChanged = UpdateAggregatesFromSets
            };
            Sets.Add(set);
        }

        UpdateAggregatesFromSets();
    }

    public void UpdateAggregatesFromSets()
    {
        if (Sets.Count == 0) return;
        LoggedSetsText = Sets.Count.ToString(CultureInfo.InvariantCulture);
        var workingSets = Sets.Where(s => !s.IsWarmup).ToList();
        var primarySets = workingSets.Count > 0 ? workingSets : Sets.ToList();
        var avgReps = (int)Math.Round(primarySets.Average(s => s.RepsCompleted));
        LoggedRepsText = avgReps.ToString(CultureInfo.InvariantCulture);
        var maxWeight = primarySets.Max(s => s.WeightKg);
        LoggedWeightText = maxWeight.ToString("F1", CultureInfo.InvariantCulture);
    }

    public void CommitEffortSliderValue(double value)
    {
        SetEffort(value, true);
    }

    private void SetEffort(double value, bool allowNeutralIfUnset)
    {
        if (!double.IsFinite(value))
            return;

        var effort = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 1d, 10d);
        // An untouched centered thumb is a neutral starting position, not a recorded rating.
        if (!allowNeutralIfUnset && !TryGetEffort(out _) && effort == 5)
            return;

        LoggedEffortText = effort.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryGetEffort(out int effort)
    {
        return int.TryParse(LoggedEffortText, NumberStyles.None, CultureInfo.InvariantCulture, out effort) &&
               effort is >= 1 and <= 10;
    }
}
