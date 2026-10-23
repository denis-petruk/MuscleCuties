using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Workout;

namespace MuscleCuties.UI.Services.Profile;

public static class ProfileWorkoutActivityOptions
{
    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityType> selected)
    {
        return WorkoutActivityOptionCatalog.Build(selected);
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

    public static bool IsStrengthActivity(WorkoutActivityType activity)
    {
        return WorkoutActivityPreferences.IsStrengthActivity(activity);
    }

    public static ObservableCollection<StrengthTrainingStyleOptionItem> BuildStrengthStyles(
        StrengthTrainingStyle selected)
    {
        return StrengthTrainingStyleOptionCatalog.Build(selected);
    }
}
