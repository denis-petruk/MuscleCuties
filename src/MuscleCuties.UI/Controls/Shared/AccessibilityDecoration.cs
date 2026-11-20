using System.Runtime.CompilerServices;
#if IOS
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// Decorative artwork may yield its column to text at accessibility sizes. The text already carries
/// the meaning; the icon only reinforces it, while its fixed width can force a long Russian word to
/// break inside itself.
/// </summary>
public static class AccessibilityDecoration
{
    public static readonly BindableProperty HideAtAccessibilitySizeProperty =
        BindableProperty.CreateAttached(
            "HideAtAccessibilitySize",
            typeof(bool),
            typeof(AccessibilityDecoration),
            false,
            propertyChanged: static (bindable, _, newValue) =>
            {
                if (bindable is VisualElement view)
                    Apply(view, newValue is true);
            });

#if IOS
    private static readonly NSString ContentSizeCategoryDidChange =
        new("UIContentSizeCategoryDidChangeNotification");
#endif

    private static readonly ConditionalWeakTable<VisualElement, Subscription> Subscriptions = new();

    public static bool GetHideAtAccessibilitySize(BindableObject view) =>
        (bool)view.GetValue(HideAtAccessibilitySizeProperty);

    public static void SetHideAtAccessibilitySize(BindableObject view, bool value) =>
        view.SetValue(HideAtAccessibilitySizeProperty, value);

    private static void Apply(VisualElement view, bool hide)
    {
        if (!hide)
        {
            if (Subscriptions.TryGetValue(view, out var existing))
            {
                Subscriptions.Remove(view);
                existing.Dispose();
            }

            view.IsVisible = true;
            return;
        }

        Update(view);
        Subscriptions.GetValue(view, static element => Subscribe(element));
    }

    private static void Update(VisualElement view) => view.IsVisible = !IsAccessibilityTextSize();

    internal static bool IsAccessibilityTextSize()
    {
#if IOS
        var category = UIApplication.SharedApplication.PreferredContentSizeCategory?.ToString();
        return category is not null && category.Contains("Accessibility", StringComparison.Ordinal);
#else
        return false;
#endif
    }

    private static Subscription Subscribe(VisualElement view)
    {
#if IOS
        var observer = NSNotificationCenter.DefaultCenter.AddObserver(
            ContentSizeCategoryDidChange,
            _ => MainThread.BeginInvokeOnMainThread(() => Update(view)));
        return new Subscription(observer);
#else
        return new Subscription();
#endif
    }

    private sealed class Subscription : IDisposable
    {
#if IOS
        private readonly NSObject? _observer;

        public Subscription(NSObject observer)
        {
            _observer = observer;
        }
#endif

        public Subscription()
        {
        }

        public void Dispose()
        {
#if IOS
            if (_observer is not null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(_observer);
#endif
        }
    }
}
