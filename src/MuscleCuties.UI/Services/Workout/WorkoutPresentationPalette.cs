using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Services.Workout;

/// <summary>Activity-tag colours for workout surfaces. The tag is a stable code, so a cardio block looks the</summary>
/// same on every screen and in both themes.</summary>
internal static class WorkoutPresentationPalette
{
    /// <summary>Which theme the next colour lookup resolves against; set when the app theme changes.</summary>
    public static bool IsDarkTheme { get; set; }

    /// <summary>Block background for an activity tag.</summary>
    public static Color Background(string tag)
    {
        var key = (IsDarkTheme, tag.ToUpperInvariant()) switch
        {
            (true, "CARDIO") => "RecoverySurfaceDark",
            (true, "RECOVERY") => "WorkoutRecoverySurfaceDark",
            (true, "REST") => "WorkoutRestSurfaceDark",
            (true, "STRENGTH") => "WorkoutStrengthSurfaceDark",
            (true, _) => "CardSurfaceDark",
            (false, "CARDIO") => "RecoverySurfaceLight",
            (false, "RECOVERY") => "WorkoutRecoverySurfaceLight",
            (false, "REST") => "WorkoutRestSurfaceLight",
            (false, "STRENGTH") => "Secondary",
            _ => "TextPrimaryDark"
        };

        return AppThemeResources.GetColor(key, default);
    }

    /// <summary>Text colour that stays readable on that background.</summary>
    public static Color Text(string tag)
    {
        var key = (IsDarkTheme, tag.ToUpperInvariant()) switch
        {
            (true, "CARDIO") => "RecoveryTextDark",
            (true, "RECOVERY") => "WorkoutRecoveryTextDark",
            (true, "REST") => "ActionSurfaceDark",
            (true, "STRENGTH") => "WorkoutStrengthTextDark",
            (true, _) => "TextTertiaryDark",
            (false, "CARDIO") => "RecoveryTextLight",
            (false, "RECOVERY") => "CyclePhaseFollicularTextLight",
            (false, "REST") => "WorkoutRestTextLight",
            (false, "STRENGTH") => "WorkoutStrengthTextLight",
            _ => "TextHeadingLight"
        };

        return AppThemeResources.GetColor(key, default);
    }
}
