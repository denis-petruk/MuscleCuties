using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class ExerciseSetsUiGroup : ObservableObject
{
    [ObservableProperty] private string _volumeSummary = string.Empty;

    public ExerciseSetsUiGroup(string exerciseName)
    {
        ExerciseName = exerciseName;
    }

    public string ExerciseName { get; }

    public ObservableCollection<WorkoutExerciseSetDto> Sets { get; } = [];
    public ObservableCollection<EditableExerciseSetItem> EditableSets { get; } = [];

    private static string FormatVolumeSummary(int workingSetCount, double totalVolumeKg) =>
        UiTextCatalog.Format(
            "Workout_History_VolumeSummaryFormat",
            workingSetCount,
            totalVolumeKg.ToString("F0", CultureInfo.CurrentCulture));

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
