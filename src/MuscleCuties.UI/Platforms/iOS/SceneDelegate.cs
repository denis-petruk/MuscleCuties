using System.Diagnostics.CodeAnalysis;
using Foundation;
using UIKit;

namespace MuscleCuties.UI;

[Register("SceneDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Standard iOS SceneDelegate class name.")]
/// <summary>Per-scene lifecycle for the privacy shield. iOS 13 and later delivers background and foreground</summary>
/// to the scene, not the app delegate, so the shield is applied here as well.</summary>
public sealed class SceneDelegate : MauiUISceneDelegate
{
    [Export("sceneWillResignActive:")]
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "iOS ObjC selector export requires instance member.")]
    /// <summary>Covers this scene as soon as it stops being front most, before the switcher snapshot is taken.</summary>
    public void WillResignActive(UIScene scene)
    {
        ApplePrivacyShieldHelper.ShowPrivacyShield(scene);
    }

    /// <summary>Tells the app the scene entered the background.</summary>
    public override void DidEnterBackground(UIScene scene)
    {
        ApplePrivacyShieldHelper.ShowPrivacyShield(scene);
        base.DidEnterBackground(scene);
    }

    [Export("sceneDidBecomeActive:")]
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "iOS ObjC selector export requires instance member.")]
    /// <summary>Removes the cover once the scene is interactive again.</summary>
    public void DidBecomeActive(UIScene scene)
    {
        ApplePrivacyShieldHelper.HidePrivacyShield(scene);
    }
}
