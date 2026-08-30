using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class WorkoutActivitySectionItem : ObservableObject
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
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string MetricText { get; init; } = string.Empty;
    public string SummaryText { get; init; } = string.Empty;
    public Color ActivityBackground { get; init; } = Colors.Transparent;
    public Color ActivityTextColor { get; init; } = Colors.Black;
    public ObservableCollection<WorkoutExerciseItem> Exercises { get; init; } = new();

    public bool HasExercises => Exercises.Count > 0;
    public bool IsLogged => Exercises.Count > 0 && Exercises.All(exercise => exercise.IsLogged);
    public string LogButtonText => IsLogged ? "Update activity" : "Log activity";
}
