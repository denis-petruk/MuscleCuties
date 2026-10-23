#if IOS || MACCATALYST
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
                BackgroundColor = UIColor.FromRGBA(43, 29, 36, 250),
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
            };
            var brand = new UILabel(cover.Bounds)
            {
                Text = "MuscleCuties",
                TextColor = UIColor.FromRGB(248, 238, 244),
                Font = UIFont.BoldSystemFontOfSize(28),
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
}
#endif
