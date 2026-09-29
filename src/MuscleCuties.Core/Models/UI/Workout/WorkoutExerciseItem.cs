using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class WorkoutExerciseItem : ObservableObject
{
    private string _loggedEffortText = string.Empty;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLogged;

    public int WorkoutDayExerciseId { get; set; }
    public int ExerciseId { get; set; }
    public string ActivityTag { get; set; } = string.Empty;
    public string ActivityTitle { get; set; } = string.Empty;
    public Color ActivityBackground { get; set; } = Colors.Transparent;
    public Color ActivityTextColor { get; set; } = Colors.Black;
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
    public string EffortLabel { get; set; } = "Effort 1-10";
    public string LoggedSetsText { get; set; } = string.Empty;
    public string LoggedRepsText { get; set; } = string.Empty;
    public string LoggedWeightText { get; set; } = string.Empty;
    public string LoggedDurationMinutesText { get; set; } = string.Empty;
    public string LoggedDistanceKmText { get; set; } = string.Empty;
    public string LoggedPaceText { get; set; } = string.Empty;
    public string LoggedHeartRateText { get; set; } = string.Empty;
    public string LoggedPowerWattsText { get; set; } = string.Empty;
    public string LoggedCadenceRpmText { get; set; } = string.Empty;
    public string LoggedEffortText
    {
        get => _loggedEffortText;
        set
        {
            if (!SetProperty(ref _loggedEffortText, value ?? string.Empty))
                return;

            OnPropertyChanged(nameof(EffortSliderValue));
            OnPropertyChanged(nameof(EffortDisplayText));
        }
    }

    public double EffortSliderValue
    {
        get => TryGetEffort(out var effort) ? effort : 5d;
        set => SetEffort(value, allowNeutralIfUnset: false);
    }

    public string EffortDisplayText => TryGetEffort(out var effort) ? $"{effort} / 10" : "Move to choose";

    public void CommitEffortSliderValue(double value) => SetEffort(value, allowNeutralIfUnset: true);

    private void SetEffort(double value, bool allowNeutralIfUnset)
    {
        if (!double.IsFinite(value))
            return;

        var effort = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 1d, 10d);
        // An untouched centered thumb is a neutral starting position, not a recorded rating.
        if (!allowNeutralIfUnset && !TryGetEffort(out _) && effort == 5)
            return;

        LoggedEffortText = effort.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(EffortSliderValue));
    }

    private bool TryGetEffort(out int effort) =>
        int.TryParse(LoggedEffortText, NumberStyles.None, CultureInfo.InvariantCulture, out effort) &&
        effort is >= 1 and <= 10;

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

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsButtonText));
    }

    partial void OnIsLoggedChanged(bool value)
    {
        OnPropertyChanged(nameof(LogButtonText));
        OnPropertyChanged(nameof(LogStatusText));
    }
}
