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

    /// <summary>Bottom strip a sheet always keeps for its pinned footer.</summary>
    private const double SheetFooterReserve = 12;

    private VisualElement? _capturedFocusTarget;
    private ContentPage? _isolationPage;
    private Page? _hookedPage;
    private long _transitionVersion;
    private bool _focusEntryPending;
    private bool _announcementDelivered;
    private Action<double>? _keyboardHandler;

    public AnimatedModalView()
    {
        IsVisible = false;
        InputTransparent = true;
        CascadeInputTransparent = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

    private void HookPage(Page? page)
    {
        if (ReferenceEquals(_hookedPage, page))
            return;

        UnhookPage(_hookedPage);
        if (page is null)
            return;

        _hookedPage = page;
        page.Disappearing += OnPageDisappearing;
    }

    private void UnhookPage(Page? page)
    {
        if (page is not null)
        {
            page.Disappearing -= OnPageDisappearing;
        }

        if (ReferenceEquals(_hookedPage, page))
            _hookedPage = null;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        ReleaseScope();
        UnhookPage(_hookedPage ?? _isolationPage ?? HostPage());
    }

    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        ReleaseScope();
    }

    private void ReleaseScope()
    {
        var page = _isolationPage ?? _hookedPage ?? HostPage();
        if (page is not null)
        {
            ModalAccessibilityScope.Release(page, this);
            ModalTabBar.Release(page);
            DetachKeyboard();
            _isolationPage = null;
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        var page = HostPage();
        HookPage(page);
        ApplyBottomSafeAreaBleed();

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
            _isolationPage = page;
            ModalAccessibilityScope.Acquire(_isolationPage, this);
            ModalTabBar.Acquire(_isolationPage);
        }

        _capturedFocusTarget ??= RestoreFocusTarget ?? FocusedOutsideSheet();
        CompleteOpen(_transitionVersion);
    }

    /// <summary>
    /// Lets the sheet reach the physical bottom edge.
    /// </summary>
    /// <remarks>
    /// Pages that set <c>SafeAreaEdges="All"</c> inset their content above the home indicator, and a
    /// sheet anchored to the bottom of that content stops one indicator-height short of the screen.
    /// Negative bottom margin extends the host over that strip; the shared footer inset keeps the
    /// footer content clear of the indicator. Pages without the inset get the same treatment, so the
    /// frame reaches the edge identically everywhere.
    /// </remarks>
    private void ApplyBottomSafeAreaBleed()
    {
#if IOS
        var bottom = 0.0;
        foreach (var scene in UIKit.UIApplication.SharedApplication.ConnectedScenes)
        {
            if (scene is not UIKit.UIWindowScene windowScene)
                continue;

            foreach (var window in windowScene.Windows)
            {
                if (window.IsKeyWindow)
                {
                    bottom = window.SafeAreaInsets.Bottom;
                    break;
                }
            }

            if (bottom > 0)
                break;
        }

        if (bottom > 0)
            Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, -bottom);
#endif
    }

    /// <summary>
    /// Shortens the sheet by the keyboard overlap so the sheet's own scroll body stays reachable.
    /// </summary>
    /// <remarks>
    /// The sheet is anchored to the bottom of the window; without this the keys covered the lower
    /// part of every modal, which is where a picker's results and a sheet's commit button live. The
    /// work is done on the surface border rather than per sheet, so every modal gets the same
    /// behaviour from the same numbers (see <see cref="ModalKeyboardInset"/>).
    /// </remarks>
    private void AttachKeyboard()
    {
        _keyboardHandler ??= OnKeyboardInsetChanged;
        ModalKeyboardInset.Subscribe(_keyboardHandler);
    }

    private void DetachKeyboard()
    {
        if (_keyboardHandler is not null)
            ModalKeyboardInset.Unsubscribe(_keyboardHandler);

        ApplySheetHeight(0);
    }

    private void OnKeyboardInsetChanged(double inset) => ApplySheetHeight(inset);

    private void ApplySheetHeight(double keyboardInset)
    {
        var surface = SheetSurface();
        if (surface is null)
            return;

        var body = EnsureScrollBody(surface);
        if (body is null)
            return;

        // Padding grows the scrollable extent by the covered strip plus the footer reserve, so the
        // last row and any pinned footer can always be scrolled clear of the keys.
        body.Padding = new Thickness(
            body.Padding.Left,
            body.Padding.Top,
            body.Padding.Right,
            Math.Max(0, keyboardInset) + SheetFooterReserve);

        // The body padding keeps scrolled rows reachable; the surface padding is what lifts a pinned
        // footer row above the keyboard toolbar. Pages such as archive detail draw their footer in the
        // sheet grid, outside the scroll body, so padding only the body left the save bar under the keys.
        var surfaceInset = keyboardInset > 0 ? keyboardInset + SheetFooterReserve : 0;
        surface.Padding = new Thickness(
            surface.Padding.Left,
            surface.Padding.Top,
            surface.Padding.Right,
            surfaceInset);

        if (keyboardInset > 0)
            RevealContentBelowFocusedField(body);
    }

    /// <summary>
    /// Scrolls the body so the row under the focused field is visible, not just the field itself.
    /// </summary>
    /// <remarks>
    /// MAUI scrolls a focused entry into view, which for the food picker put the field at the bottom
    /// of the visible strip and left the result list behind the keyboard. Anchoring the focused row at
    /// the top of the body gives the picker its first result back and keeps a pinned footer reachable.
    /// </remarks>
    private static void RevealContentBelowFocusedField(ScrollView body)
    {
        var focused = FindFocusedDescendant(body);
        if (focused is null)
            return;

        body.Dispatcher.Dispatch(async () =>
        {
            try
            {
                await body.ScrollToAsync(focused, ScrollToPosition.Start, true);
            }
            catch (Exception)
            {
                // A body that is being replaced while the keyboard animates must not crash the sheet.
            }
        });
    }

    private static VisualElement? FindFocusedDescendant(IVisualTreeElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is VisualElement { IsFocused: true } focused)
                return focused;

            if (FindFocusedDescendant(child) is { } nested)
                return nested;
        }

        return null;
    }

    /// <summary>
    /// Guarantees the sheet has one scrollable body.
    /// </summary>
    /// <remarks>
    /// Nine sheets drew their content straight into the surface with no scroll view, so shortening the
    /// sheet for the keyboard had nowhere to scroll and their last rows stayed unreachable. The frame
    /// wraps the surface content once, on first open, instead of each sheet repeating the wrapper; a
    /// sheet that already ships its own body (the food picker) keeps it.
    /// </remarks>
    private static ScrollView? EnsureScrollBody(Border surface)
    {
        if (surface.Content is null)
            return null;

        // A sheet that already owns a body keeps it — wrapping it again gives the sheet two
        // scroll views and the inner one stops scrolling. The check-in sheet is the case that
        // matters: it draws a header, its own ScrollView and a pinned footer into the surface.
        var ownBody = FindScrollBody(surface.Content);
        if (ownBody is not null)
            return ownBody;

        var body = new ScrollView
        {
            Content = surface.Content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        surface.Content = body;
        return body;
    }

    private static ScrollView? FindScrollBody(IVisualTreeElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is ScrollView scroll)
                return scroll;

            if (FindScrollBody(child) is { } nested)
                return nested;
        }

        return null;
    }

    private Border? SheetSurface()
    {
        var style = Application.Current?.Resources.TryGetValue("ModalSurface", out var resource) == true
            ? resource as Style
            : null;

        return Descendants()
            .OfType<Border>()
            .FirstOrDefault(border => style is null || ReferenceEquals(border.Style, style));
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
        HookPage(_isolationPage);
        ModalAccessibilityScope.Acquire(_isolationPage, this);
        ModalTabBar.Acquire(_isolationPage);
        AttachKeyboard();
        ModalTransition.PlayShow(this);

        // Layout of a content-sized sheet completes after this property change. Focus and the
        // title announcement are completed again from OnSizeAllocated or Loaded if not ready yet.
        Dispatcher.Dispatch(() => CompleteOpen(version));
    }

    private void Close(long version)
    {
        InputTransparent = true;
        CascadeInputTransparent = true;
        ReleaseScope();
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

    /// <summary>
    /// Finishes a pending open presentation synchronously and reports whether the sheet is now
    /// open. Sheet-to-sheet handoffs use it so the outgoing sheet never exposes the page beneath.
    /// </summary>
    public bool CompleteOpenTransition()
    {
        CompleteOpen(_transitionVersion);
        return IsOpen;
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
/// Scope acquisition and release are instance-tracked and idempotent.
/// </summary>
internal static class ModalAccessibilityScope
{
    private static readonly ConditionalWeakTable<Page, Scope> Scopes = new();

    internal static void Acquire(Page? page, VisualElement modal)
    {
        if (page is null || modal is null)
            return;

        var scope = Scopes.GetValue(page, static _ => new Scope());
        scope.Acquire(modal);
    }

    internal static void Release(Page? page, VisualElement? modal = null)
    {
        if (page is null || !Scopes.TryGetValue(page, out var scope))
            return;

        scope.Release(modal);
    }

    private sealed class Scope
    {
        private readonly HashSet<VisualElement> _activeModals = new();
        private readonly List<(VisualElement Element, bool? Excluded)> _isolated = new();

        internal int Count => _activeModals.Count;

        internal void Acquire(VisualElement modal)
        {
            if (!_activeModals.Add(modal))
                return;

            if (_activeModals.Count == 1)
                Isolate(modal);
        }

        internal void Release(VisualElement? modal)
        {
            if (modal is not null)
            {
                if (!_activeModals.Remove(modal))
                    return;
            }
            else
            {
                _activeModals.Clear();
            }

            if (_activeModals.Count == 0)
                Restore();
        }

        private void Isolate(VisualElement modal)
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

        private void Restore()
        {
            for (var index = _isolated.Count - 1; index >= 0; index--)
            {
                var (element, previous) = _isolated[index];
                element.SetValue(AutomationProperties.ExcludedWithChildrenProperty, previous ?? false);
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
