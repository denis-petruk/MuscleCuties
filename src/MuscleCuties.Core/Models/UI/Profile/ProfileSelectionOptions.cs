using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Profile;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Common;

namespace MuscleCuties.Core.Models.UI.Profile;

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

    public static IReadOnlyList<SelectionOption<FeedbackTopic>> FeedbackTopics { get; } =
    [
        new(FeedbackTopic.DesignOrStyle, "Design or style", "SparkleCircle24"),
        new(FeedbackTopic.SomethingBroken, "Something broken", "Bug24"),
        new(FeedbackTopic.Nutrition, "Nutrition", "Food24"),
        new(FeedbackTopic.Workout, "Workout", "Dumbbell24"),
        new(FeedbackTopic.CycleTracking, "Cycle tracking", "HeartCircle24"),
        new(FeedbackTopic.Onboarding, "Onboarding", "PersonPill24"),
        new(FeedbackTopic.NewIdea, "New idea", "ChatBubblesQuestion24")
    ];

    public static IReadOnlyList<SelectionOption<FeedbackPriority>> FeedbackPriorities { get; } =
    [
        new(FeedbackPriority.NiceToImprove, "Nice to improve", "SparkleCircle24"),
        new(FeedbackPriority.Annoying, "Annoying", "ChatWarning24"),
        new(FeedbackPriority.BlockingMe, "Blocking me", "ShieldError24"),
        new(FeedbackPriority.TinyPolish, "Tiny polish", "TargetEdit24")
    ];

    public static bool UsesWeightGoalPace(UserGoal goal) =>
        goal is UserGoal.FatLoss or UserGoal.Strength;
}
