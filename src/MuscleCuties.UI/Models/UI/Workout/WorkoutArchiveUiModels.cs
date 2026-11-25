using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>The sets of one exercise in a finished session, grouped so the history page can expand them.</summary>
public partial class ExerciseSetsUiGroup : ObservableObject
{
    [ObservableProperty] private string _volumeSummary = string.Empty;

    /// <summary>Creates the group for a movement; the sets are added afterwards, in order.</summary>
    public ExerciseSetsUiGroup(string exerciseName)
    {
        ExerciseName = exerciseName;
    }

    /// <summary>The exercise's name.</summary>
    public string ExerciseName { get; }

    /// <summary>The saved sets of the exercise.</summary>
    public ObservableCollection<WorkoutExerciseSetDto> Sets { get; } = [];
    /// <summary>The editable sets of the exercise.</summary>
    public ObservableCollection<EditableExerciseSetItem> EditableSets { get; } = [];

    /// <summary>The volume line shown under the exercise.</summary>
    private static string FormatVolumeSummary(int workingSetCount, double totalVolumeKg) =>
        UiTextCatalog.Format(
            "Workout_History_VolumeSummaryFormat",
            workingSetCount,
            DisplayFormat.WholeNumber(totalVolumeKg));

    /// <summary>Recomputes the volume line from whichever set collection is in use (read-only or editable),</summary>
    /// counting working sets only.</summary>
    public void RefreshSummary()
    {
        if (EditableSets.Count > 0)
        {
            var working = EditableSets.Where(s => !s.IsWarmup).ToList();
            var totalVol = working.Sum(s => s.WeightKg * s.RepsCompleted);
            VolumeSummary = FormatVolumeSummary(working.Count, totalVol);
        }
        else
        {
            var workingSets = Sets.Where(s => !s.IsWarmup).ToList();
            var totalVol = Sets.Where(s => !s.IsWarmup).Sum(s => s.WeightKg * s.RepsCompleted);
            VolumeSummary = FormatVolumeSummary(workingSets.Count, totalVol);
        }
    }
}
