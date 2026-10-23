using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Services.Workout;

internal static class WorkoutPresentationPalette
{
    public static bool IsDarkTheme { get; set; }

    public static Color Background(string tag)
    {
        if (IsDarkTheme)
            return tag.ToUpperInvariant() switch
            {
                "CARDIO" => Color.FromArgb("#1A3A38"),
                "RECOVERY" => Color.FromArgb("#1E3520"),
                "REST" => Color.FromArgb("#2E2B26"),
                "STRENGTH" => Color.FromArgb("#3D2435"),
                _ => Color.FromArgb("#3A2931")
            };
        return tag.ToUpperInvariant() switch
        {
            "CARDIO" => Color.FromArgb("#E0F2F1"),
            "RECOVERY" => Color.FromArgb("#E8F5E9"),
            "REST" => Color.FromArgb("#F0EFEA"),
            "STRENGTH" => Color.FromArgb("#F8DFF1"),
            _ => Color.FromArgb("#F8EEF4")
        };
    }

    public static Color Text(string tag)
    {
        if (IsDarkTheme)
            return tag.ToUpperInvariant() switch
            {
                "CARDIO" => Color.FromArgb("#7ECBC5"),
                "RECOVERY" => Color.FromArgb("#8FBF8F"),
                "REST" => Color.FromArgb("#F0B8CF"),
                "STRENGTH" => Color.FromArgb("#D98AAF"),
                _ => Color.FromArgb("#AE8D9B")
            };
        return tag.ToUpperInvariant() switch
        {
            "CARDIO" => Color.FromArgb("#1F6F68"),
            "RECOVERY" => Color.FromArgb("#3A6B3A"),
            "REST" => Color.FromArgb("#5F5A50"),
            "STRENGTH" => Color.FromArgb("#8D3A5F"),
            _ => Color.FromArgb("#5B4650")
        };
    }
}
