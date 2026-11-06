using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Accessibility;

[assembly:
    SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
        Target = "~N:MuscleCuties.UI.Controls.Shared", Justification = "Shared UI controls namespace.")]

namespace MuscleCuties.UI.Controls.Shared;

public class AnimatedModalView : ContentView
{
    public static readonly BindableProperty IsOpenProperty =
        BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(AnimatedModalView), false,
            propertyChanged: OnIsOpenChanged);

    public static readonly BindableProperty RestoreFocusTargetProperty =
        BindableProperty.Create(nameof(RestoreFocusTarget), typeof(VisualElement), typeof(AnimatedModalView));

    private VisualElement? _capturedFocusTarget;
    private ContentPage? _isolationPage;
    private long _transitionVersion;
    private bool _focusEntryPending;
    private bool _announcementDelivered;

    public AnimatedModalView()
    {
        IsVisible = false;
        InputTransparent = true;
        CascadeInputTransparent = true;
        Loaded += OnLoaded;
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Optional control that receives focus after the sheet closes. When it is not supplied,
    /// the control that was focused before the sheet opened is restored instead.
    /// </summary>
    public VisualElement? RestoreFocusTarget
    {
        get => (VisualElement?)GetValue(RestoreFocusTargetProperty);
        set => SetValue(RestoreFocusTargetProperty, value);
    }

    protected virtual void PrepareForOpen()
    {
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (IsOpen)
            CompleteOpen(_transitionVersion);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (!IsOpen)
        {
            IsVisible = false;
            InputTransparent = true;
            CascadeInputTransparent = true;
            return;
        }

        // A sheet can be opened before its page attaches it. Finish the parts that need the
        // page tree as soon as the sheet is loaded.
        if (_isolationPage is null)
        {
            _isolationPage = HostPage();
            ModalAccessibilityScope.Acquire(_isolationPage, this);
        }

        _capturedFocusTarget ??= RestoreFocusTarget ?? FocusedOutsideSheet();
        CompleteOpen(_transitionVersion);
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not AnimatedModalView modal)
            return;

        var open = (bool)newValue;
        var version = ++modal._transitionVersion;
        if (open)
        {
            modal.Open(version);
        }
        else
        {
            modal.Close(version);
        }
    }

    private void Open(long version)
    {
        PrepareForOpen();
        _capturedFocusTarget = RestoreFocusTarget ?? FocusedOutsideSheet();
        _focusEntryPending = true;
        _announcementDelivered = false;

        IsVisible = true;
        InputTransparent = false;
        CascadeInputTransparent = false;
        _isolationPage = HostPage();
        ModalAccessibilityScope.Acquire(_isolationPage, this);
        ModalTransition.PlayShow(this);

        // Layout of a content-sized sheet completes after this property change. Focus and the
        // title announcement are completed again from OnSizeAllocated or Loaded if not ready yet.
        Dispatcher.Dispatch(() => CompleteOpen(version));
    }

    private void Close(long version)
    {
        InputTransparent = true;
        CascadeInputTransparent = true;
        ModalAccessibilityScope.Release(_isolationPage);
        _isolationPage = null;
        ModalTransition.PlayHide(this, () =>
        {
            // A hide that lost a race with a newer open/close must not restore stale state.
            if (version != _transitionVersion || IsOpen)
                return;

            IsVisible = false;
            RestoreFocus();
            _focusEntryPending = false;
            _capturedFocusTarget = null;
        });
    }

    private void CompleteOpen(long version)
    {
        if (version != _transitionVersion || !IsOpen)
            return;

        AnnounceTitleOnce();

        if (!_focusEntryPending)
            return;

        var first = FirstFocusableControl();
        if (first is null)
            return;

        _focusEntryPending = false;
        first.Focus();
    }

    private void AnnounceTitleOnce()
    {
        if (_announcementDelivered)
            return;

        var title = Descendants()
            .OfType<Label>()
            .Where(IsVisibleInTree)
            .Select(label => new
            {
                Label = label,
                Level = SemanticProperties.GetHeadingLevel(label),
                Text = label.Text?.Trim()
            })
            .FirstOrDefault(item => item.Level != SemanticHeadingLevel.None &&
                                    !string.IsNullOrWhiteSpace(item.Text));

        if (title is null)
            return;

        _announcementDelivered = true;
        SemanticScreenReader.Default.Announce(title.Text!);
    }

    private void RestoreFocus()
    {
        foreach (var focused in Descendants().Where(element => element.IsFocused).ToArray())
            focused.Unfocus();

        var target = RestoreFocusTarget ?? _capturedFocusTarget;
        if (target is { IsEnabled: true } && IsVisibleInTree(target))
            target.Focus();
    }

    /// <summary>
    /// Captures the focused control outside this sheet before the sheet becomes visible. MAUI
    /// exposes no global focused-element query, so the host page's content is traversed directly.
    /// </summary>
    private VisualElement? FocusedOutsideSheet()
    {
        var page = HostPage();
        if (page?.Content is not VisualElement content)
            return null;

        if (content.IsFocused)
            return content;

        return Descendants(content, this).FirstOrDefault(element => element.IsFocused);
    }

    private ContentPage? HostPage()
    {
        for (Element? current = this; current is not null; current = current.Parent)
        {
            if (current is ContentPage page)
                return page;
        }

        return null;
    }

    private VisualElement? FirstFocusableControl()
    {
        return Descendants()
            .FirstOrDefault(element =>
                (element is Entry or Editor or Button or SearchBar)
                && IsVisibleInTree(element)
                && element.IsEnabled);
    }

    private static bool IsVisibleInTree(VisualElement element)
    {
        for (Element? current = element; current is not null; current = current.Parent)
        {
            if (current is VisualElement ancestor && !ancestor.IsVisible)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Walks the sheet's own content. MAUI has no public descendant query on <see cref="VisualElement"/>,
    /// so the layout and content containers used by the sheets are traversed explicitly.
    /// </summary>
    private IEnumerable<VisualElement> Descendants()
    {
        if (Content is not VisualElement root)
            yield break;

        foreach (var descendant in Descendants(root, null))
            yield return descendant;
    }

    private static IEnumerable<VisualElement> Descendants(VisualElement root, VisualElement? excluded)
    {
        foreach (var child in EnumerateChildren(root))
        {
            if (ReferenceEquals(child, excluded))
                continue;

            yield return child;
            foreach (var descendant in Descendants(child, excluded))
                yield return descendant;
        }
    }

    private static IEnumerable<VisualElement> EnumerateChildren(VisualElement element)
    {
        switch (element)
        {
            // Checked before Layout: a ScrollView is both a content view and a layout, and its
            // fields live in Content rather than in the layout's children collection.
            case IContentView { Content: VisualElement content }:
                yield return content;
                break;
            case Layout layout:
                foreach (var child in layout.Children)
                {
                    if (child is VisualElement visual)
                        yield return visual;
                }

                break;
        }
    }
}

/// <summary>
/// Reference-counts covered-content isolation per page. Two overlapping sheets share one set of
/// captured siblings, so closing either one cannot make the page reachable while the other is open.
/// </summary>
internal static class ModalAccessibilityScope
{
    private static readonly ConditionalWeakTable<Page, Scope> Scopes = new();

    internal static void Acquire(Page? page, VisualElement modal)
    {
        if (page is null)
            return;

        var scope = Scopes.GetValue(page, static _ => new Scope());
        if (scope.Count++ == 0)
            scope.Isolate(modal);
    }

    internal static void Release(Page? page)
    {
        if (page is null || !Scopes.TryGetValue(page, out var scope) || scope.Count == 0)
            return;

        scope.Count--;
        if (scope.Count == 0)
            scope.Restore();
    }

    private sealed class Scope
    {
        private readonly List<(VisualElement Element, bool? Excluded)> _isolated = new();

        internal int Count { get; set; }

        internal void Isolate(VisualElement modal)
        {
            Element child = modal;
            Element? parent = modal.Parent;
            while (parent is VisualElement parentVisual && parentVisual is not Page)
            {
                foreach (var sibling in EnumerateChildren(parentVisual))
                {
                    if (ReferenceEquals(sibling, child))
                        continue;

                    var previous = (bool?)sibling.GetValue(AutomationProperties.ExcludedWithChildrenProperty);
                    _isolated.Add((sibling, previous));
                    sibling.SetValue(AutomationProperties.ExcludedWithChildrenProperty, true);
                }

                child = parentVisual;
                parent = parentVisual.Parent;
            }
        }

        internal void Restore()
        {
            for (var index = _isolated.Count - 1; index >= 0; index--)
            {
                var (element, previous) = _isolated[index];
                if (previous is { } value)
                    element.SetValue(AutomationProperties.ExcludedWithChildrenProperty, value);
                else
                    element.ClearValue(AutomationProperties.ExcludedWithChildrenProperty);
            }

            _isolated.Clear();
        }

        private static IEnumerable<VisualElement> EnumerateChildren(VisualElement element)
        {
            switch (element)
            {
                case IContentView { Content: VisualElement content }:
                    yield return content;
                    break;
                case Layout layout:
                    foreach (var child in layout.Children)
                    {
                        if (child is VisualElement visual)
                            yield return visual;
                    }

                    break;
            }
        }
    }
}
