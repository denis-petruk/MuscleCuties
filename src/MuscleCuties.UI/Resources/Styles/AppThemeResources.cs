using Microsoft.Maui.Controls;
using CyclePhaseDto = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;

namespace MuscleCuties.UI.Resources.Styles;

/// <summary>The single door to the theme's colours and dimensions, so no control has to know a resource key's light and</summary>
/// dark pairs itself.</summary>
public static class AppThemeResources
{
    /// <summary>Whether the app is in dark mode right now, which is what picks one half of a light/dark pair.</summary>
    public static bool IsDark => (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark;

    /// <summary>The theme's numeric resource for the key.</summary>
    public static double GetDouble(string key) => GetRequired<double>(key);

    /// <summary>The theme's thickness resource for the key.</summary>
    public static Thickness GetThickness(string key) => GetRequired<Thickness>(key);

    /// <summary>The theme's string resource for the key.</summary>
    public static string GetString(string key) => GetRequired<string>(key);

    /// <summary>A resource that must exist; a missing key is a programming error, not something to fall back from silently.</summary>
    public static T GetRequired<T>(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is T resource)
            return resource;

        throw new InvalidOperationException($"Required app style resource '{key}' is missing or has an unexpected type.");
    }

    /// <summary>Registers a cache to be dropped when the resources are reloaded, so a theme change never keeps serving the</summary>
    /// previous theme's colour.</summary>
    public static void RegisterCacheClearer(Action clear)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => clear();
    }

    /// <summary>The colour for the current theme from a light/dark pair, with a fallback for a key the theme is missing.</summary>
    public static Color GetColor(string lightKey, string darkKey, Color? fallback = null)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return GetColor(theme, lightKey, darkKey, fallback);
    }

    /// <summary>The same choice made for an explicit theme rather than the current one, for previews and thumbnails.</summary>
    public static Color GetColor(AppTheme theme, string lightKey, string darkKey, Color? fallback = null)
    {
        var key = theme == AppTheme.Dark ? darkKey : lightKey;
        return GetColor(key, fallback);
    }

    /// <summary>
    /// The one cycle-phase theme mapping: background tone by default, "Text" tone for foreground use.
    /// Both cycle converters used to carry their own copy of this switch.
    /// </summary>
    public static Color GetCyclePhaseColor(CyclePhaseDto phase, bool textTone = false)
    {
        var suffix = textTone ? "Text" : string.Empty;
        var (lightKey, darkKey) = phase switch
        {
            CyclePhaseDto.Menstrual => ($"CyclePhaseMenstrual{suffix}Light", $"CyclePhaseMenstrual{suffix}Dark"),
            CyclePhaseDto.Follicular => ($"CyclePhaseFollicular{suffix}Light", $"CyclePhaseFollicular{suffix}Dark"),
            CyclePhaseDto.Ovulatory => ($"CyclePhaseOvulatory{suffix}Light", $"CyclePhaseOvulatory{suffix}Dark"),
            CyclePhaseDto.Luteal => ($"CyclePhaseLuteal{suffix}Light", $"CyclePhaseLuteal{suffix}Dark"),
            _ => textTone ? ("TextPrimary", "TextPrimaryDark") : ("Transparent", "Transparent")
        };

        return GetColor(lightKey, darkKey);
    }

    /// <summary>A colour by a single key, used where the theme already defines one value for both modes.</summary>
    public static Color GetColor(string key, Color? fallback = null)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            if (value is Color color)
                return color;

            if (value is SolidColorBrush brush)
                return brush.Color;
        }

        return fallback ?? GetRequired<Color>("Transparent");
    }
}
