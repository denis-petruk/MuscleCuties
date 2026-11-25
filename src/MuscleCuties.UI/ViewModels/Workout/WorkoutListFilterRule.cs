namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>
/// Which workouts a list filter may show. A chip carries a stable tag and a localized label;
/// only the tag may decide visibility, because the label changes with the language and can never
/// equal "All" or a workout tag such as "STRENGTH".
/// </summary>
public static class WorkoutListFilterRule
{
    /// <summary>The unfiltered chip: every scheduled workout of the selected week.</summary>
    public const string AllTag = "ALL";

    // The stable activity codes the overview sends. A UI view model may not import the Core
    // domain, so they are repeated here exactly as WorkoutActivityIcons does, and a test pins
    // them to Core's canonical values.
    public const string StrengthTag = "STRENGTH";
    public const string CardioTag = "CARDIO";
    public const string RecoveryTag = "RECOVERY";

    /// <summary>Whether a scheduled day belongs in the filtered list. A rest day never does — it is context in the week</summary>
    /// strip, not a workout — and an unset filter shows everything else.</summary>
    public static bool IsVisible(bool isRestDay, string? workoutTag, string? filterTag)
    {
        // A rest day is week context in the schedule strip, never a tappable workout card.
        if (isRestDay)
            return false;

        if (string.IsNullOrEmpty(filterTag) ||
            string.Equals(filterTag, AllTag, StringComparison.OrdinalIgnoreCase))
            return true;

        var tag = workoutTag ?? string.Empty;
        return string.Equals(filterTag, RecoveryTag, StringComparison.OrdinalIgnoreCase)
            ? tag.Contains(RecoveryTag, StringComparison.OrdinalIgnoreCase)
            : string.Equals(tag, filterTag, StringComparison.OrdinalIgnoreCase);
    }
}
