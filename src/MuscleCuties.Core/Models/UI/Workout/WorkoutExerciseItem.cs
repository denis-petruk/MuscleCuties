namespace MuscleCuties.Core.Models.UI.Workout;

public class WorkoutExerciseItem
{
    public int WorkoutDayExerciseId { get; set; }
    public int ExerciseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string TechniqueNotes { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public string PreviousText { get; set; } = string.Empty;
    public string RecommendationText { get; set; } = string.Empty;
    public bool UsesEnduranceMetrics { get; set; }
    public bool UsesStrengthMetrics => !UsesEnduranceMetrics;
    public bool UsesDurationMetric { get; set; }
    public bool UsesDistanceMetric { get; set; }
    public bool UsesPaceMetric { get; set; }
    public bool UsesHeartRateMetric { get; set; }
    public bool UsesDurationOnlyMetric =>
        UsesDurationMetric &&
        !UsesDistanceMetric &&
        !UsesPaceMetric &&
        !UsesHeartRateMetric;
    public bool UsesCardioMetricGrid =>
        UsesEnduranceMetrics &&
        !UsesDurationOnlyMetric;
    public bool UsesWeight { get; set; } = true;
    public string LoggedSetsText { get; set; } = string.Empty;
    public string LoggedRepsText { get; set; } = string.Empty;
    public string LoggedWeightText { get; set; } = string.Empty;
    public string LoggedDurationMinutesText { get; set; } = string.Empty;
    public string LoggedDistanceKmText { get; set; } = string.Empty;
    public string LoggedPaceText { get; set; } = string.Empty;
    public string LoggedHeartRateText { get; set; } = string.Empty;
}
