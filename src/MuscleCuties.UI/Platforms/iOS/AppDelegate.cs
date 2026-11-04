using System.Diagnostics.CodeAnalysis;
using Foundation;
using UIKit;

namespace MuscleCuties.UI;

[Register("AppDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Standard iOS AppDelegate class name.")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

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
