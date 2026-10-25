using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Services.Workout;

internal static class WorkoutPresentationPalette
{
    public static bool IsDarkTheme { get; set; }

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

    public static Color Text(string tag)
    {
        var key = (IsDarkTheme, tag.ToUpperInvariant()) switch
        {
            (true, "CARDIO") => "RecoveryTextDark",
            (true, "RECOVERY") => "WorkoutRecoveryTextDark",
            (true, "REST") => "ActionSurfaceDark",
            (true, "STRENGTH") => "WorkoutStrengthTextDark",
            (true, _) => "Gray500",
            (false, "CARDIO") => "RecoveryTextLight",
            (false, "RECOVERY") => "CyclePhaseFollicularTextLight",
            (false, "REST") => "WorkoutRestTextLight",
            (false, "STRENGTH") => "WorkoutStrengthTextLight",
            _ => "DeepMauve"
        };

        return AppThemeResources.GetColor(key, default);
    }
}
