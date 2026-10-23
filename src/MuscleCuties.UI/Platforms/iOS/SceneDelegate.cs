using System.Diagnostics.CodeAnalysis;
using Foundation;
using UIKit;

namespace MuscleCuties.UI;

[Register("SceneDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Standard iOS SceneDelegate class name.")]
public sealed class SceneDelegate : MauiUISceneDelegate
{
    [Export("sceneWillResignActive:")]
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "iOS ObjC selector export requires instance member.")]
    public void WillResignActive(UIScene scene)
    {
        ApplePrivacyShieldHelper.ShowPrivacyShield(scene);
    }

    public override void DidEnterBackground(UIScene scene)
    {
        ApplePrivacyShieldHelper.ShowPrivacyShield(scene);
        base.DidEnterBackground(scene);
    }

    [Export("sceneDidBecomeActive:")]
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "iOS ObjC selector export requires instance member.")]
    public void DidBecomeActive(UIScene scene)
    {
        ApplePrivacyShieldHelper.HidePrivacyShield(scene);
    }
}
