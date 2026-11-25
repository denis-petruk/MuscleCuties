using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>The training activities offered on the workout-preferences screen, built from the shared catalog so the</summary>
/// list and its labels match the rest of the app.</summary>
public static class ProfileWorkoutActivityOptions
{
    /// <summary>The activity options with the given ones selected.</summary>
    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityDto> selected)
    {
        return WorkoutActivityOptionCatalog.Build(selected);
    }

    /// <summary>The options grouped into the sections the picker shows.</summary>
    public static IReadOnlyList<WorkoutActivityGroupSection> BuildGroups(
        ObservableCollection<WorkoutActivityOptionItem> options)
    {
        return WorkoutActivityOptionCatalog.BuildGroups(options);
    }

    /// <summary>Toggles the option and returns the status line for the change.</summary>
    public static string ToggleSelection(
        ObservableCollection<WorkoutActivityOptionItem> options,
        WorkoutActivityOptionItem item)
    {
        return WorkoutActivityOptionCatalog.ToggleSelection(options, item);
    }

}
