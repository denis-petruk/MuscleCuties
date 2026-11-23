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

    public static void SetIsSectionHeading(BindableObject view, bool value) =>
        view.SetValue(IsSectionHeadingProperty, value);

    public static void AttachSectionTitle(Label label)
    {
        SectionTitleSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize18", "PresentationAccessibilitySectionTitleMaxFontSize"));
    }

    public static bool GetIsCompactControl(BindableObject view) =>
        (bool)view.GetValue(IsCompactControlProperty);

    public static void SetIsCompactControl(BindableObject view, bool value) =>
        view.SetValue(IsCompactControlProperty, value);

    public static void AttachCompactPicker(Picker picker)
    {
        CompactPickerSubscriptions.GetValue(picker, static item =>
            AttachPicker(item, "UiFontSize16", "PresentationAccessibilityCompactControlMaxFontSize"));
    }

    public static bool GetIsCompactLabel(BindableObject view) =>
        (bool)view.GetValue(IsCompactLabelProperty);

    public static void SetIsCompactLabel(BindableObject view, bool value) =>
        view.SetValue(IsCompactLabelProperty, value);

    public static void AttachCompactLabel(Label label)
    {
        CompactLabelSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize17", "PresentationAccessibilityCompactLabelMaxFontSize"));
    }

    public static bool GetIsCompactChoiceLabel(BindableObject view) =>
        (bool)view.GetValue(IsCompactChoiceLabelProperty);

    public static void SetIsCompactChoiceLabel(BindableObject view, bool value) =>
        view.SetValue(IsCompactChoiceLabelProperty, value);

    public static void AttachCompactChoiceLabel(Label label)
    {
        CompactChoiceSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize15", "PresentationAccessibilityCompactLabelMaxFontSize"));
    }

    public static bool GetIsPhasePill(BindableObject view) =>
        (bool)view.GetValue(IsPhasePillProperty);

    public static void SetIsPhasePill(BindableObject view, bool value) =>
        view.SetValue(IsPhasePillProperty, value);

    public static void AttachPhasePillLabel(Label label)
    {
        PhasePillSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize16", "PresentationAccessibilityPhasePillMaxFontSize"));
    }

    public static bool GetIsBodyLead(BindableObject view) =>
        (bool)view.GetValue(IsBodyLeadProperty);

    public static void SetIsBodyLead(BindableObject view, bool value) =>
        view.SetValue(IsBodyLeadProperty, value);

    public static void AttachBodyLeadLabel(Label label)
    {
        BodyLeadSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize17", "PresentationAccessibilityBodyLeadMaxFontSize"));
    }

    public static bool GetIsModalTitle(BindableObject view) =>
        (bool)view.GetValue(IsModalTitleProperty);

    public static void SetIsModalTitle(BindableObject view, bool value) =>
        view.SetValue(IsModalTitleProperty, value);

    public static void AttachModalTitle(Label label)
    {
        ModalTitleSubscriptions.GetValue(label, static item =>
            Attach(item, "UiFontSize20", "PresentationAccessibilityModalTitleMaxFontSize"));
    }

    public static IDisposable Attach(Label label, string baseSizeToken, string maximumSizeToken) =>
        Attach(label, baseSizeToken, AppThemeResources.GetDouble(maximumSizeToken));

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
    private static EmptySubscription AttachPicker(Picker picker, string baseSizeToken,
        string maximumSizeToken)
    {
        ApplyPicker(picker, AppThemeResources.GetDouble(baseSizeToken),
            AppThemeResources.GetDouble(maximumSizeToken));
        return EmptySubscription.Instance;
    }
#endif

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
    private sealed class NotificationSubscription(NSObject observer) : IDisposable
    {
        public void Dispose() => NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
    }
#else
    private sealed class EmptySubscription : IDisposable
    {
        public static readonly EmptySubscription Instance = new();

        public void Dispose()
        {
        }
    }
#endif
}
