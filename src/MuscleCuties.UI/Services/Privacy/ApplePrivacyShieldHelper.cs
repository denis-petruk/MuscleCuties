#if IOS || MACCATALYST
using Microsoft.Maui.Graphics;
using UIKit;

namespace MuscleCuties.UI;

/// <summary>Covers the app's windows with a branded, nearly opaque surface while the app is being</summary>
/// backgrounded, so the OS task switcher cannot show the previous screen's content.</summary>
public static class ApplePrivacyShieldHelper
{
    private const int PrivacyShieldTag = 20491214;

    /// <summary>Covers every window of the scene once; a second call is a no-op on a window that already</summary>
    /// carries the shield.</summary>
    public static void ShowPrivacyShield(UIScene? scene = null)
    {
        foreach (var window in Windows(scene))
        {
            if (window.ViewWithTag(PrivacyShieldTag) is not null)
                continue;

            var shield = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterial))
            {
                Frame = window.Bounds,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                Tag = PrivacyShieldTag
            };
            // A nearly opaque branded surface prevents text from remaining legible
            // even when a system blur style changes or a snapshot is compressed.
            var cover = new UIView(shield.Bounds)
            {
                BackgroundColor = ResolveColor("PrivacyShieldSurface"),
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
            };
            var brand = new UILabel(cover.Bounds)
            {
                Text = "MuscleCuties",
                TextColor = ResolveColor("TextPrimaryDark"),
                Font = UIFont.BoldSystemFontOfSize((nfloat)AppThemeResources.GetDouble("UiFontSize28")),
                TextAlignment = UITextAlignment.Center,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                AccessibilityLabel = "MuscleCuties privacy screen"
            };
            cover.AddSubview(brand);
            shield.ContentView.AddSubview(cover);
            window.AddSubview(shield);
        }
    }

    /// <summary>Removes the shield from every window of the scene. Safe to call when none is shown.</summary>
    public static void HidePrivacyShield(UIScene? scene = null)
    {
        foreach (var window in Windows(scene))
            window.ViewWithTag(PrivacyShieldTag)?.RemoveFromSuperview();
    }

    /// <summary>The scene's windows, or every connected window scene's windows when no scene is given.</summary>
    private static IEnumerable<UIWindow> Windows(UIScene? scene)
    {
        if (scene is UIWindowScene windowScene)
            return windowScene.Windows;

        return UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(connectedScene => connectedScene.Windows);
    }

    /// <summary>Reads a theme colour by key and converts it for UIKit, so the shield follows the theme.</summary>
    private static UIColor ResolveColor(string key)
    {
        var color = AppThemeResources.GetRequired<Color>(key);
        return UIColor.FromRGBA(
            (nfloat)color.Red,
            (nfloat)color.Green,
            (nfloat)color.Blue,
            (nfloat)color.Alpha);
    }
}
#endif
