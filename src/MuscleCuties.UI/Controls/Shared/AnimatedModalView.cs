using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Accessibility;

[assembly:
    SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
        Target = "~N:MuscleCuties.UI.Controls.Shared", Justification = "Shared UI controls namespace.")]

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>The base every sheet is built on: it shows and hides its surface with a transition, keeps focus and the</summary>
/// screen reader inside itself while it is open, and gives its content the height the keyboard leaves.</summary>
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

    /// <summary>Starts hidden — a sheet is only ever shown through its open state — and wires the load and unload hooks that</summary>
    /// tie it to its page.</summary>
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

    /// <summary>
    ///     The one place a sheet decides how tall it grows. Subclasses that simply fill or fit their
    ///     content declare a policy here instead of each repeating the same size-allocated override.
    /// </summary>
    protected virtual SheetSizing SheetSizingPolicy => SheetSizing.None;

    /// <summary>The hook a sheet uses to refresh its contents before every showing, including a reopen it did not ask</summary>
    /// for itself.</summary>
    protected virtual void PrepareForOpen()
    {
    }

    /// <summary>Completes an open transition once the sheet has a size.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (IsOpen)
            CompleteOpen(_transitionVersion);

        // Sizing runs after the open transition so a sheet that focuses a field on open still measures
        // the same viewport the subclass-owned override used to.
        ApplySheetSizing(height);
    }

    /// <summary>Applies the declared sizing policy to the sheet surface, if the sheet has one.</summary>
    private void ApplySheetSizing(double viewportHeight)
    {
        if (SheetSizingPolicy == SheetSizing.None || SheetSurface() is not { } surface)
            return;

        if (SheetSizingPolicy == SheetSizing.ContentFit)
            BottomSheetSizing.ApplyContentFit(surface, viewportHeight);
        else
            BottomSheetSizing.Apply(surface, viewportHeight);
    }

    /// <summary>Listens to the page the sheet was added to, so the page is released when it leaves the screen.</summary>
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

    /// <summary>Detaches from a page, guarded so the same page is never detached twice.</summary>
    private void UnhookPage(Page? page)
    {
        if (page is not null)
        {
            page.Disappearing -= OnPageDisappearing;
        }

        if (ReferenceEquals(_hookedPage, page))
            _hookedPage = null;
    }

    /// <summary>Releases the page when the sheet is removed from the visual tree, so a torn-down sheet cannot hold the page</summary>
    /// in its modal state.</summary>
    private void OnUnloaded(object? sender, EventArgs e)
    {
        ReleaseScope();
        UnhookPage(_hookedPage ?? _isolationPage ?? HostPage());
    }

    /// <summary>Releases the page when it disappears underneath the sheet, so navigating away never leaves the page's controls</summary>
    /// unreachable.</summary>
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        ReleaseScope();
    }

    /// <summary>Hands the page back once nothing owns it any more.</summary>
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

    /// <summary>Attaches the sheet when it enters the tree: page isolation, keyboard tracking, focus and its own sizing.</summary>
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

    /// <summary>Stops following keyboard changes, so a closed sheet is not resized on every inset.</summary>
    private void DetachKeyboard()
    {
        if (_keyboardHandler is not null)
            ModalKeyboardInset.Unsubscribe(_keyboardHandler);

        ApplySheetHeight(0);
    }

    /// <summary>Fits the sheet again when the keyboard inset changes.</summary>
    private void OnKeyboardInsetChanged(double inset) => ApplySheetHeight(inset);

    /// <summary>Sizes the surface to the viewport minus the keyboard, so a focused field can never end up behind it.</summary>
    private void ApplySheetHeight(double keyboardInset)
    {
        var surface = SheetSurface();
        if (surface is null)
            return;

        var inset = Math.Max(0, keyboardInset);
        var body = EnsureScrollBody(surface);

        // The surface padding below shrinks the body viewport above the keys, so the body itself only
        // needs the footer strip. Adding the whole keyboard height here as well let the scroll view
        // over-scroll a bottom field above its own viewport, where the user could not see it while
        // typing.
        if (body is not null)
        {
            body.Padding = new Thickness(
                body.Padding.Left,
                body.Padding.Top,
                body.Padding.Right,
                SheetFooterReserve);
        }

        // The surface padding is what lifts a pinned footer row above the keyboard. It is applied
        // even when the sheet's body is a CollectionView rather than a ScrollView: those sheets still
        // need their footer and results lifted, they just scroll themselves.
        surface.Padding = new Thickness(
            surface.Padding.Left,
            surface.Padding.Top,
            surface.Padding.Right,
            inset > 0 ? inset + SheetFooterReserve : 0);

        if (inset > 0 && body is not null)
            RevealContentBelowFocusedField(body);
    }

    /// <summary>
    /// Scrolls the body so the focused field is fully visible, not just its bottom line.
    /// </summary>
    /// <remarks>
    /// MAUI scrolls a focused entry into view, which for the food picker put the field at the bottom
    /// of the visible strip and left the result list behind the keyboard. Centering the field keeps it
    /// clear of both the header and the keyboard, leaves the rows around it reachable, and still keeps
    /// a pinned footer on screen.
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
                // Centering keeps the whole field on screen instead of pinning it to the body's top
                // edge, where a short viewport could clip its first line above the header.
                await body.ScrollToAsync(focused, ScrollToPosition.Center, true);
            }
            catch (Exception)
            {
                // A body that is being replaced while the keyboard animates must not crash the sheet.
            }
        });
    }

    /// <summary>The field inside the sheet that currently holds focus, if any.</summary>
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

        // A CollectionView is already a scroll body. Wrapping it would nest two scrollers and
        // steal the sheet's scroll gestures, which is what the meal editor and suggestion sheets
        // would do if the frame only recognised ScrollView.
        if (HasVisibleItemsView(surface.Content))
            return null;

        var body = new ScrollView
        {
            Content = surface.Content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        surface.Content = body;
        return body;
    }

    /// <summary>The vertical body the sheet scrolls, preferring a visible one so a hidden template is never padded instead of</summary>
    /// the real content.</summary>
    private static ScrollView? FindScrollBody(IVisualTreeElement root)
    {
        // Prefer a visible vertical body. A sheet can contain horizontal rails (meal types) and
        // hidden state panels; either one used to be mistaken for the body, so the keyboard inset
        // padded the wrong control while the real content stayed put.
        return FindVerticalScrollBody(root, visibleOnly: true)
               ?? FindVerticalScrollBody(root, visibleOnly: false);
    }

    /// <summary>The first vertical scroll view under a root, optionally only when it is visible.</summary>
    private static ScrollView? FindVerticalScrollBody(IVisualTreeElement root, bool visibleOnly)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is ScrollView { Orientation: not ScrollOrientation.Horizontal } scroll &&
                (!visibleOnly || IsVisibleInTree(scroll)))
            {
                return scroll;
            }

            if (FindVerticalScrollBody(child, visibleOnly) is { } nested)
                return nested;
        }

        return null;
    }

    /// <summary>Whether a visible list is present, which decides whether the sheet scrolls itself or leaves that to the</summary>
    /// list.</summary>
    private static bool HasVisibleItemsView(IVisualTreeElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is ItemsView items && IsVisibleInTree(items))
                return true;

            if (HasVisibleItemsView(child))
                return true;
        }

        return false;
    }

    /// <summary>The border that carries the sheet's surface style and therefore its height.</summary>
    private Border? SheetSurface()
    {
        var style = Application.Current?.Resources.TryGetValue("ModalSurface", out var resource) == true
            ? resource as Style
            : null;

        return Descendants()
            .OfType<Border>()
            .FirstOrDefault(border => style is null || ReferenceEquals(border.Style, style));
    }

    /// <summary>Runs the open or close transition for the new state.</summary>
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

    /// <summary>Runs the opening transition and schedules the focus and the announcement for after layout, because a</summary>
    /// content-sized sheet is not measurable yet at this point.</summary>
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

    /// <summary>Runs the closing transition, leaving the sheet hidden and its content no longer able to take touches.</summary>
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

    /// <summary>Applies the focus and title announcement that need the final layout, ignoring a transition that a newer open or</summary>
    /// close has already superseded.</summary>
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

    /// <summary>Reads the sheet's title to the screen reader once per opening, so it does not repeat on every relayout.</summary>
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

    /// <summary>Gives focus back to the control that had it before the sheet opened, so the page continues where the user left</summary>
    /// it.</summary>
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

    /// <summary>The page this sheet is hosted on.</summary>
    private ContentPage? HostPage()
    {
        for (Element? current = this; current is not null; current = current.Parent)
        {
            if (current is ContentPage page)
                return page;
        }

        return null;
    }

    /// <summary>The first control in the sheet that can take focus, used when it opens without a direction.</summary>
    private VisualElement? FirstFocusableControl()
    {
        return Descendants()
            .FirstOrDefault(element =>
                (element is Entry or Editor or Button or SearchBar)
                && IsVisibleInTree(element)
                && element.IsEnabled);
    }

    /// <summary>Whether an element and every ancestor of it are visible, so the focus search skips the hidden templates a</summary>
    /// sheet keeps for other states.</summary>
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

    /// <summary>Every visual descendant except one subtree, so an ongoing search does not descend into itself.</summary>
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

    /// <summary>The element's children, including the ones a plain visual-children walk misses for list views and pickers.</summary>
    internal static IEnumerable<VisualElement> EnumerateChildren(VisualElement element)
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

    /// <summary>Marks the page as owned by this sheet, so everything behind it is out of reach while it is open.</summary>
    internal static void Acquire(Page? page, VisualElement modal)
    {
        if (page is null || modal is null)
            return;

        var scope = Scopes.GetValue(page, static _ => new Scope());
        scope.Acquire(modal);
    }

    /// <summary>Gives the page back, optionally only when this sheet is still the one that owns it.</summary>
    internal static void Release(Page? page, VisualElement? modal = null)
    {
        if (page is null || !Scopes.TryGetValue(page, out var scope))
            return;

        scope.Release(modal);
    }

    /// <summary>What one page remembers while sheets are open on it: what was disabled, what had focus, and how many sheets</summary>
    /// are stacked on it.</summary>
    private sealed class Scope
    {
        private readonly List<VisualElement> _activeModals = new();
        private readonly List<(VisualElement Element, bool? Excluded)> _isolated = new();

        /// <summary>How many modals are open right now.</summary>
        internal int Count => _activeModals.Count;

        /// <summary>Counts a sheet in and, on the first one, records what the page looked like so it can be put back exactly.</summary>
        internal void Acquire(VisualElement modal)
        {
            if (_activeModals.Contains(modal))
                return;

            _activeModals.Add(modal);
            Reisolate();
        }

        /// <summary>Counts a sheet out, restoring the page only when the last one closes and only for the sheet that actually owns</summary>
        /// it.</summary>
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

            Reisolate();
        }

        /// <summary>
        /// Keeps exactly the topmost sheet reachable when modals overlap during a handoff. The
        /// previous scope isolated only the first sheet's siblings, so the editor opened above the
        /// chooser stayed inside an excluded subtree and disappeared from the accessibility tree.
        /// </summary>
        private void Reisolate()
        {
            Restore();
            if (_activeModals.Count > 0)
                Isolate(_activeModals[^1]);
        }

        /// <summary>Makes everything on the page except the sheet unreachable to touch and to the screen reader.</summary>
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

        /// <summary>Puts the page back the way it was before the first sheet opened.</summary>
        private void Restore()
        {
            for (var index = _isolated.Count - 1; index >= 0; index--)
            {
                var (element, previous) = _isolated[index];
                element.SetValue(AutomationProperties.ExcludedWithChildrenProperty, previous ?? false);
            }

            _isolated.Clear();
        }

        /// <summary>Enumerates the modal's child elements.</summary>
        private static IEnumerable<VisualElement> EnumerateChildren(VisualElement element) =>
            AnimatedModalView.EnumerateChildren(element);
    }
}
