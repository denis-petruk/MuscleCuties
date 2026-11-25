using UIKit;

namespace MuscleCuties.UI;

/// <summary>Process entry point for iOS; every platform behaviour lives in <see cref="AppDelegate" />.</summary>
public class Program
{
    /// <summary>Hands the process to UIKit with the app delegate that builds the MAUI app.</summary>
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
