using System.Runtime.CompilerServices;
#if IOS
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Keeps a display text role readable at accessibility sizes: the role still scales, but only
///     up to a reviewed ceiling. A long word no longer breaks inside itself and the header cannot
///     crowd the page's next action. Body and supporting text are never capped.
/// </summary>
public static class CappedTextSize
{
    public static readonly BindableProperty IsSectionHeadingProperty = BindableProperty.CreateAttached(
        "IsSectionHeading",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachSectionTitle(label);
        });

    public static readonly BindableProperty IsPhasePillProperty = BindableProperty.CreateAttached(
        "IsPhasePill",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachPhasePillLabel(label);
        });

    public static readonly BindableProperty IsModalTitleProperty = BindableProperty.CreateAttached(
        "IsModalTitle",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachModalTitle(label);
        });

    public static readonly BindableProperty IsBodyLeadProperty = BindableProperty.CreateAttached(
        "IsBodyLead",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachBodyLeadLabel(label);
        });

    public static readonly BindableProperty IsCompactControlProperty = BindableProperty.CreateAttached(
        "IsCompactControl",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Picker picker && newValue is true)
                AttachCompactPicker(picker);
        });

    public static readonly BindableProperty IsCompactLabelProperty = BindableProperty.CreateAttached(
        "IsCompactLabel",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachCompactLabel(label);
        });

    public static readonly BindableProperty IsCompactChoiceLabelProperty = BindableProperty.CreateAttached(
        "IsCompactChoiceLabel",
        typeof(bool),
        typeof(CappedTextSize),
        false,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Label label && newValue is true)
                AttachCompactChoiceLabel(label);
        });

    private static readonly ConditionalWeakTable<Label, IDisposable> SectionTitleSubscriptions = new();
    private static readonly ConditionalWeakTable<Label, IDisposable> CompactLabelSubscriptions = new();
    private static readonly ConditionalWeakTable<Label, IDisposable> CompactChoiceSubscriptions = new();
    private static readonly ConditionalWeakTable<Picker, IDisposable> CompactPickerSubscriptions = new();
    private static readonly ConditionalWeakTable<Label, IDisposable> BodyLeadSubscriptions = new();
    private static readonly ConditionalWeakTable<Label, IDisposable> PhasePillSubscriptions = new();
    private static readonly ConditionalWeakTable<Label, IDisposable> ModalTitleSubscriptions = new();

#if IOS
    private static readonly NSString ContentSizeCategoryDidChange =
        new("UIContentSizeCategoryDidChangeNotification");
#endif

    /// <summary>
    ///     Applies the ceiling now and keeps it in step with later Dynamic Type changes. The
    ///     caller owns the returned subscription and disposes it with the view's lifetime.
    /// </summary>
    public static bool GetIsSectionHeading(BindableObject view) =>
        (bool)view.GetValue(IsSectionHeadingProperty);

    /// <summary>Sets whether the label is styled as a section heading.</summary>
    public static void SetIsSectionHeading(BindableObject view, bool value) =>
        view.SetValue(IsSectionHeadingProperty, value);

    /// <summary>Keeps a section heading inside its size cap as its text and the user's text size change.</summary>
    public static void AttachSectionTitle(Label label)
    {
        SectionTitleSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize18", "PresentationAccessibilitySectionTitleMaxFontSize"));
    }

    /// <summary>True when the control uses the compact text cap.</summary>
    public static bool GetIsCompactControl(BindableObject view) =>
        (bool)view.GetValue(IsCompactControlProperty);

    /// <summary>Sets whether the control uses the compact text cap.</summary>
    public static void SetIsCompactControl(BindableObject view, bool value) =>
        view.SetValue(IsCompactControlProperty, value);

    /// <summary>The cap for a compact picker, so a long option shrinks instead of wrapping the row.</summary>
    public static void AttachCompactPicker(Picker picker)
    {
        CompactPickerSubscriptions.GetValue(picker, static item =>
            AttachPicker(item, "UiFontSize16", "PresentationAccessibilityCompactControlMaxFontSize"));
    }

    /// <summary>True when the label uses the compact text cap.</summary>
    public static bool GetIsCompactLabel(BindableObject view) =>
        (bool)view.GetValue(IsCompactLabelProperty);

    /// <summary>Sets whether the label uses the compact text cap.</summary>
    public static void SetIsCompactLabel(BindableObject view, bool value) =>
        view.SetValue(IsCompactLabelProperty, value);

    /// <summary>The cap for a compact label, whose row has no room to grow.</summary>
    public static void AttachCompactLabel(Label label)
    {
        CompactLabelSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize17", "PresentationAccessibilityCompactLabelMaxFontSize"));
    }

    /// <summary>True when the label is a compact choice caption.</summary>
    public static bool GetIsCompactChoiceLabel(BindableObject view) =>
        (bool)view.GetValue(IsCompactChoiceLabelProperty);

    /// <summary>Sets whether the label is a compact choice caption.</summary>
    public static void SetIsCompactChoiceLabel(BindableObject view, bool value) =>
        view.SetValue(IsCompactChoiceLabelProperty, value);

    /// <summary>The cap for a choice label inside a chip or segment, where wrapping would break the control's shape.</summary>
    public static void AttachCompactChoiceLabel(Label label)
    {
        CompactChoiceSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize15", "PresentationAccessibilityCompactLabelMaxFontSize"));
    }

    /// <summary>True when the label is a cycle phase pill.</summary>
    public static bool GetIsPhasePill(BindableObject view) =>
        (bool)view.GetValue(IsPhasePillProperty);

    /// <summary>Sets whether the label is a cycle phase pill.</summary>
    public static void SetIsPhasePill(BindableObject view, bool value) =>
        view.SetValue(IsPhasePillProperty, value);

    /// <summary>The cap for the phase pill's text, which is always drawn on one line.</summary>
    public static void AttachPhasePillLabel(Label label)
    {
        PhasePillSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize16", "PresentationAccessibilityPhasePillMaxFontSize"));
    }

    /// <summary>True when the label is an introductory body line.</summary>
    public static bool GetIsBodyLead(BindableObject view) =>
        (bool)view.GetValue(IsBodyLeadProperty);

    /// <summary>Sets whether the label is an introductory body line.</summary>
    public static void SetIsBodyLead(BindableObject view, bool value) =>
        view.SetValue(IsBodyLeadProperty, value);

    /// <summary>The cap for a lead paragraph, which may be large but not turn into a headline at accessibility sizes.</summary>
    public static void AttachBodyLeadLabel(Label label)
    {
        BodyLeadSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize17", "PresentationAccessibilityBodyLeadMaxFontSize"));
    }

    /// <summary>True when the label is a modal title.</summary>
    public static bool GetIsModalTitle(BindableObject view) =>
        (bool)view.GetValue(IsModalTitleProperty);

    /// <summary>Sets whether the label is a modal title.</summary>
    public static void SetIsModalTitle(BindableObject view, bool value) =>
        view.SetValue(IsModalTitleProperty, value);

    /// <summary>The cap for a sheet's title.</summary>
    public static void AttachModalTitle(Label label)
    {
        ModalTitleSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize20", "PresentationAccessibilityModalTitleMaxFontSize"));
    }

    /// <summary>Caps a label's font at the theme's maximum for the token pair.</summary>
    public static IDisposable Attach(Label label, string baseSizeToken, string maximumSizeToken) =>
        Attach(label, baseSizeToken, AppThemeResources.GetDouble(maximumSizeToken));

    /// <summary>Watches a label's text and the user's text size, re-applying the cap on every change and returning the handle</summary>
    /// that stops watching.</summary>
    public static IDisposable Attach(Label label, string baseSizeToken, double maximumSize)
    {
        var baseSize = AppThemeResources.GetDouble(baseSizeToken);
        Apply(label, baseSize, maximumSize);
#if IOS
        var observer = NSNotificationCenter.DefaultCenter.AddObserver(
            ContentSizeCategoryDidChange,
            _ => MainThread.BeginInvokeOnMainThread(() => Apply(label, baseSize, maximumSize)));
        return new NotificationSubscription(observer);
#else
        return EmptySubscription.Instance;
#endif
    }

#if IOS
    /// <summary>Caps a picker's font and re-applies it when the text size changes.</summary>
    private static NotificationSubscription AttachPicker(Picker picker, string baseSizeToken,
        string maximumSizeToken)
    {
        var baseSize = AppThemeResources.GetDouble(baseSizeToken);
        var maximumSize = AppThemeResources.GetDouble(maximumSizeToken);
        ApplyPicker(picker, baseSize, maximumSize);
        var observer = NSNotificationCenter.DefaultCenter.AddObserver(
            ContentSizeCategoryDidChange,
            _ => MainThread.BeginInvokeOnMainThread(() => ApplyPicker(picker, baseSize, maximumSize)));
        return new NotificationSubscription(observer);
    }
#else
    /// <summary>Caps a picker's font once, as this platform sends no size changes.</summary>
    private static EmptySubscription AttachPicker(Picker picker, string baseSizeToken,
        string maximumSizeToken)
    {
        ApplyPicker(picker, AppThemeResources.GetDouble(baseSizeToken),
            AppThemeResources.GetDouble(maximumSizeToken));
        return EmptySubscription.Instance;
    }
#endif

    /// <summary>Sets a picker's font size between its base and its cap.</summary>
    private static void ApplyPicker(Picker picker, double baseSize, double maximumSize)
    {
#if IOS
        var scaledSize = (double)UIFontMetrics.DefaultMetrics.GetScaledValue((nfloat)baseSize);
        if (scaledSize > maximumSize)
        {
            picker.FontAutoScalingEnabled = false;
            picker.FontSize = maximumSize;
        }
        else
        {
            picker.FontAutoScalingEnabled = true;
            picker.FontSize = baseSize;
        }
#else
        picker.FontSize = baseSize;
#endif
    }

    /// <summary>Sets a label's font size to fit its text between its base and its cap.</summary>
    private static void Apply(Label label, double baseSize, double maximumSize)
    {
#if IOS
        // The scaled value is what the label would render before the cap. When it exceeds the
        // ceiling, pin the label at the ceiling; below it, leave normal scaling intact.
        var scaledSize = (double)UIFontMetrics.DefaultMetrics.GetScaledValue((nfloat)baseSize);
        if (scaledSize > maximumSize)
        {
            label.FontAutoScalingEnabled = false;
            label.FontSize = maximumSize;
        }
        else
        {
            label.FontAutoScalingEnabled = true;
            label.FontSize = baseSize;
        }
#else
        label.FontSize = baseSize;
#endif
    }

#if IOS
    /// <summary>Removes the text-size observer when the control that owns it goes away.</summary>
    private sealed class NotificationSubscription(NSObject observer) : IDisposable
    {
        /// <summary>Stops observing the system text-size notification.</summary>
        public void Dispose() => NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
    }
#else
    /// <summary>A handle for a control whose cap needed no observer.</summary>
    private sealed class EmptySubscription : IDisposable
    {
        public static readonly EmptySubscription Instance = new();

        /// <summary>Does nothing because this platform needs no subscription.</summary>
        public void Dispose()
        {
        }
    }
#endif
}
