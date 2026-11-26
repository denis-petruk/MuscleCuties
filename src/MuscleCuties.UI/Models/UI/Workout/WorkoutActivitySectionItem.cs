using System.Collections;
using Microsoft.Maui.Graphics;

using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One activity block inside a session, enumerable over its exercises so a collection view can</summary>
/// bind the block and its rows from a single item.</summary>
public class WorkoutActivitySectionItem : ObservableObject, IEnumerable<WorkoutExerciseItem>
{
    /// <summary>Position of the block in the session, counting from one.</summary>
    public int OrderIndex { get; init; }
    /// <summary>How many blocks the session has, which decides the start/finish wording.</summary>
    public int TotalActivities { get; init; }

    /// <summary>Activity tag, which selects the glyph and the routing rule.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Glyph for the block's activity tag.</summary>
    public string IconGlyph => WorkoutActivityIcons.ForTag(Tag);

    /// <summary>Activity title shown on the block header.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Short description of what the block asks for.</summary>
    public string Subtitle { get; init; } = string.Empty;
    /// <summary>Primary metric of the block, already formatted (duration, distance or load).</summary>
    public string MetricText { get; init; } = string.Empty;
    /// <summary>Secondary summary, such as the prescription in one line.</summary>
    public string SummaryText { get; init; } = string.Empty;
    /// <summary>Generated prescription behind the block; null when the block was not planned.</summary>
    public ActivitySessionPrescriptionOutputV020? Prescription { get; init; }
    /// <summary>Planner activity behind the block, used when the tag alone is ambiguous.</summary>
    public MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto ActivityType { get; init; }
        = MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto.Other;
    /// <summary>Block background tint.</summary>
    public Color ActivityBackground { get; init; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>The text colour used for this activity's section.</summary>
    public Color ActivityTextColor { get; init; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>The exercises in this section.</summary>
    public ObservableCollection<WorkoutExerciseItem> Exercises { get; init; } = new();

    /// <summary>True when the section has exercises.</summary>
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
    /// <summary>A block counts as logged only when every movement in it has a log.</summary>
    public bool IsLogged => Exercises.Count > 0 && Exercises.All(exercise => exercise.IsLogged);
    /// <summary>Action label, reworded once the block has a complete log.</summary>
    public string LogButtonText => IsLogged ? "Update activity" : "Log activity";

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>Enumerates the block's exercises, which is what lets a view bind rows to the block itself.</summary>
    public IEnumerator<WorkoutExerciseItem> GetEnumerator()
    {
        return Exercises.GetEnumerator();
    }
}
