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

    /// <summary>True when the view hides at accessibility text sizes.</summary>
    public static bool GetHideAtAccessibilitySize(BindableObject view) =>
        (bool)view.GetValue(HideAtAccessibilitySizeProperty);

    /// <summary>Sets whether the view hides at accessibility text sizes.</summary>
    public static void SetHideAtAccessibilitySize(BindableObject view, bool value) =>
        view.SetValue(HideAtAccessibilitySizeProperty, value);

    /// <summary>Hides purely decorative elements from the screen reader (or shows them), so a reader does not announce</summary>
    /// shapes that carry no meaning.</summary>
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

    /// <summary>Shows the view only when text size is not an accessibility size.</summary>
    private static void Update(VisualElement view) => view.IsVisible = !IsAccessibilityTextSize();

    /// <summary>Whether the user has chosen a large accessibility text size, which layouts adapt to.</summary>
    internal static bool IsAccessibilityTextSize()
    {
#if IOS
        var category = UIApplication.SharedApplication.PreferredContentSizeCategory?.ToString();
        return category is not null && category.Contains("Accessibility", StringComparison.Ordinal);
#else
        return false;
#endif
    }

    /// <summary>Keeps a decoration's hidden state in step with the text-size setting, returning the handle that stops it.</summary>
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

    /// <summary>The handle that removes the text-size observer when the control goes away.</summary>
    private sealed class Subscription : IDisposable
    {
#if IOS
        private readonly NSObject? _observer;

        /// <summary>Wraps the platform observer so disposing really unregisters it.</summary>
        public Subscription(NSObject observer)
        {
            _observer = observer;
        }
#endif

        /// <summary>A handle with nothing to remove, for a control that needed no observer.</summary>
        public Subscription()
        {
        }

        /// <summary>Stops observing the system text-size notification.</summary>
        public void Dispose()
        {
#if IOS
            if (_observer is not null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(_observer);
#endif
        }
    }
}
