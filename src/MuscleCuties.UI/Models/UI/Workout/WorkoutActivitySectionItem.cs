using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Models.UI.Workout;

public class WorkoutActivitySectionItem : ObservableObject, IEnumerable<WorkoutExerciseItem>
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
    public ActivitySessionPrescriptionOutputV020? Prescription { get; init; }
    public Color ActivityBackground { get; init; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color ActivityTextColor { get; init; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    public ObservableCollection<WorkoutExerciseItem> Exercises { get; init; } = new();

    public bool HasExercises => Exercises.Count > 0;

    /// <summary>
    /// Single-rule routing. A row-less non-rest section (including climbing, which the planner stores as
    /// a strength-type day without rows) runs the shared timed lifecycle. A section with movement rows
    /// keeps the exercise flow so distance, pace, heart-rate and power actuals stay recordable. Rest days
    /// route to no-start. See <see cref="MuscleCuties.UI.Services.Workout.TimedActivityProgress.DecideSurface"/>.
    /// </summary>
    public bool IsTimedActivity =>
        MuscleCuties.UI.Services.Workout.TimedActivityProgress.DecideSurface(Tag, HasExercises) ==
        MuscleCuties.UI.Services.Workout.TimedActivityProgress.WorkoutActivitySurface.TimedActivity;
    public bool IsLogged => Exercises.Count > 0 && Exercises.All(exercise => exercise.IsLogged);
    public string LogButtonText => IsLogged ? "Update activity" : "Log activity";

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<WorkoutExerciseItem> GetEnumerator()
    {
        return Exercises.GetEnumerator();
    }
}
