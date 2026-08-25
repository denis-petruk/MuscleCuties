namespace MuscleCuties.Core.Models.UI.Workout;

public sealed record WorkoutSessionDetail(
    int WorkoutDayId,
    string Title,
    string Subtitle,
    string SummaryText,
    IReadOnlyList<WorkoutExerciseItem> Exercises,
    bool IsRestDay = false);
