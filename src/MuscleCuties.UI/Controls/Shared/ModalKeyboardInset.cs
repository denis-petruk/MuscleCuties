using System.Runtime.CompilerServices;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// Reports how much of the window the on-screen keyboard covers.
/// </summary>
/// <remarks>
/// A sheet is anchored to the bottom of the window, so an open keyboard covered the lower part of it:
/// the food picker's result list and the meal editor's commit button sat under the keys. The sheet
/// frame asks this tracker for the overlap and shortens itself by that amount, which makes the
/// sheet's own scroll body reachable instead of covering it. Tracking lives here rather than in each
/// sheet so every modal gets the same treatment from the same numbers.
/// </remarks>
public static class ModalKeyboardInset
{
    private static readonly List<Action<double>> Subscribers = [];
    private static double _current;
    private static bool _observing;

    /// <summary>The overlap in device-independent units; 0 when the keyboard is hidden.</summary>
    public static double Current => Volatile.Read(ref _current);

    /// <summary>Subscribes a sheet; the callback fires on every change and once immediately.</summary>
    public static void Subscribe(Action<double> onChanged)
    {
        if (!Subscribers.Contains(onChanged))
            Subscribers.Add(onChanged);

        StartObserving();
        onChanged(Current);
    }

    /// <summary>Unsubscribes a sheet that closed.</summary>
    public static void Unsubscribe(Action<double> onChanged) => Subscribers.Remove(onChanged);

    private static void StartObserving()
    {
#if IOS
        if (_observing)
            return;

        _observing = true;
        UIKit.UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) => OnWillChangeFrame(args));
        UIKit.UIKeyboard.Notifications.ObserveWillHide((_, _) => Publish(0));
#endif
    }

#if IOS
    private static void OnWillChangeFrame(UIKit.UIKeyboardEventArgs args)
    {
        var screen = UIKit.UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIKit.UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow);

        if (screen is null)
        {
            return;
        }

        var frame = args.FrameEnd;
        var overlap = screen.Bounds.Height - frame.Top;
        Publish(overlap > 1 ? overlap / UIKit.UIScreen.MainScreen.Scale : 0);
    }
#endif

    private static void Publish(double overlap)
    {
        if (Math.Abs(overlap - Current) < 0.5)
            return;

        Volatile.Write(ref _current, overlap);

        foreach (var subscriber in Subscribers.ToArray())
            subscriber(overlap);
    }
}
