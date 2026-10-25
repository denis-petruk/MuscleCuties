#if IOS || MACCATALYST
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using UIKit;

namespace MuscleCuties.UI;

public static class ApplePrivacyShieldHelper
{
    private const int PrivacyShieldTag = 20491214;

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

    public static void HidePrivacyShield(UIScene? scene = null)
    {
        foreach (var window in Windows(scene))
            window.ViewWithTag(PrivacyShieldTag)?.RemoveFromSuperview();
    }

    private static IEnumerable<UIWindow> Windows(UIScene? scene)
    {
        if (scene is UIWindowScene windowScene)
            return windowScene.Windows;

        return UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(connectedScene => connectedScene.Windows);
    }

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
