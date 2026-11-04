namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// Shared motion preference. Sheets never animate when the owner has requested Reduce Motion.
/// </summary>
internal static class MotionSettings
{
    internal static bool ReduceMotion
    {
        get
        {
#if IOS
            try
            {
                return UIKit.UIAccessibility.IsReduceMotionEnabled;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
