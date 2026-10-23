using Foundation;
using UIKit;

namespace MuscleCuties.UI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnResignActivation(UIApplication application)
    {
        base.OnResignActivation(application);
        ApplePrivacyShieldHelper.ShowPrivacyShield();
    }

    public override void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        ApplePrivacyShieldHelper.ShowPrivacyShield();
    }

    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);
        ApplePrivacyShieldHelper.HidePrivacyShield();
    }
}
