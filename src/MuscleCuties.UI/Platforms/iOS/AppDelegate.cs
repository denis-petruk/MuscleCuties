using System.Diagnostics.CodeAnalysis;
using Foundation;
using UIKit;

namespace MuscleCuties.UI;

[Register("AppDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Standard iOS AppDelegate class name.")]
/// <summary>iOS application entry point: builds the MAUI app, covers the screen while the app is not front</summary>
/// most, and forwards <c>musclecuties://</c> links to Shell.</summary>
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <summary>Creates the app the platform runs.</summary>
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    /// <summary>Tells the app it is leaving the foreground.</summary>
    public override void OnResignActivation(UIApplication application)
    {
        base.OnResignActivation(application);
        ApplePrivacyShieldHelper.ShowPrivacyShield();
    }

    /// <summary>Tells the app it entered the background.</summary>
    public override void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        ApplePrivacyShieldHelper.ShowPrivacyShield();
    }

    /// <summary>Tells the app it is active again.</summary>
    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);
        ApplePrivacyShieldHelper.HidePrivacyShield();
    }

    /// <summary>Handles a URL the system opened.</summary>
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        if (url is not null && url.Scheme == "musclecuties")
        {
            var host = url.Host;
            if (!string.IsNullOrEmpty(host) && Shell.Current is { } shell)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await shell.GoToAsync(host);
                    }
                    catch
                    {
                        // Ignore unrecognized deep link route
                    }
                });
                return true;
            }
        }

        return url is not null && base.OpenUrl(application, url, options);
    }
}
