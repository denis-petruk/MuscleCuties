using System.Linq;
using Foundation;
using UIKit;

namespace MuscleCuties.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private const int PrivacyShieldTag = 20491214;

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    public override void OnResignActivation(UIApplication application)
    {
        base.OnResignActivation(application);
        ShowPrivacyShield();
    }

    public override void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        ShowPrivacyShield();
    }

    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);
        HidePrivacyShield();
    }

    private static UIWindow? GetActiveWindow() =>
        UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow);

    private static void ShowPrivacyShield()
    {
        var window = GetActiveWindow();
        if (window is null || window.ViewWithTag(PrivacyShieldTag) is not null)
            return;

        var blur = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterial);
        var shield = new UIVisualEffectView(blur)
        {
            Frame = window.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            Tag = PrivacyShieldTag
        };

        window.AddSubview(shield);
    }

    private static void HidePrivacyShield()
    {
        GetActiveWindow()?.ViewWithTag(PrivacyShieldTag)?.RemoveFromSuperview();
    }
}
