using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Common;

namespace MuscleCuties.Core.ViewModels.Profile;

internal static class ProfileSelectionOptions
{
    public static IReadOnlyList<SelectionOption<UserGoal>> Goals { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    public static IReadOnlyList<SelectionOption<WeightGoalPace>> WeightGoalPaces { get; } =
    [
        new(WeightGoalPace.Steady, "Steady"),
        new(WeightGoalPace.Aggressive, "Aggressive")
    ];

    public static IReadOnlyList<SelectionOption<CycleTrackingMode>> CycleLoggingModes { get; } =
    [
        new(CycleTrackingMode.ManualPhaseLogging, "Manual"),
        new(CycleTrackingMode.FloConnector, "Flo"),
        new(CycleTrackingMode.LunarConnector, "Lunar")
    ];

    public static bool UsesWeightGoalPace(UserGoal goal) =>
        goal is UserGoal.FatLoss or UserGoal.Strength;
}
