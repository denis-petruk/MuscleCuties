namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed record TodaysWorkoutSummary(
    string Title,
    string Subtitle,
    string DurationText,
    string ExercisesCount,
    string Intensity,
    string SessionProgressText,
    string ActivityTag = WorkoutActivityClassifier.RestTag)
{
    public static TodaysWorkoutSummary RestDay { get; } =
        new(
            "Living happy life",
            "Pure rest day",
            "Rest day",
            "0",
            "None",
            "REST",
            WorkoutActivityClassifier.RestTag);
}
