using System.Collections.ObjectModel;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Services.Profile;

public static class ProfileWorkoutActivityOptions
{
    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityDto> selected,
        bool allowInlinePreferenceConfiguration = true)
    {
        return WorkoutActivityOptionCatalog.Build(selected, allowInlinePreferenceConfiguration);
    }

    public static IReadOnlyList<WorkoutActivityGroupSection> BuildGroups(
        ObservableCollection<WorkoutActivityOptionItem> options)
    {
        return WorkoutActivityOptionCatalog.BuildGroups(options);
    }

    public static string ToggleSelection(
        ObservableCollection<WorkoutActivityOptionItem> options,
        WorkoutActivityOptionItem item)
    {
        return WorkoutActivityOptionCatalog.ToggleSelection(options, item);
    }

}
