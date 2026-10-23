using System.Collections;
using System.Collections.ObjectModel;

namespace MuscleCuties.Core.Models.UI.Workout;

public class WorkoutActivitySectionItem : IEnumerable<WorkoutExerciseItem>
{
    public int OrderIndex { get; init; }
    public int TotalActivities { get; init; }

    public string OrderText => TotalActivities <= 1
        ? "Activity block"
        : OrderIndex <= 1
            ? "Start here"
            : OrderIndex >= TotalActivities
                ? "Finish here"
                : "Then continue";

    public string Tag { get; init; } = string.Empty;

    public string IconGlyph => Tag switch
    {
        "STRENGTH" => "Dumbbell24",
        "CARDIO" => "PulseSquare24",
        "RECOVERY" => "LeafThree24",
        "REST" => "WeatherMoon24",
        _ => "Sport24"
    };

    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string MetricText { get; init; } = string.Empty;
    public string SummaryText { get; init; } = string.Empty;
    public string ActivityBackground { get; init; } = string.Empty;
    public string ActivityTextColor { get; init; } = string.Empty;
    public ObservableCollection<WorkoutExerciseItem> Exercises { get; init; } = new();

    public bool HasExercises => Exercises.Count > 0;
    public bool IsLogged => Exercises.Count > 0 && Exercises.All(exercise => exercise.IsLogged);
    public string LogButtonText => IsLogged ? "Update activity" : "Log activity";

    public IEnumerator<WorkoutExerciseItem> GetEnumerator()
    {
        return Exercises.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
