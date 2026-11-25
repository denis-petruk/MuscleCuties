using System.Runtime.CompilerServices;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// Keeps the Shell tab bar out from under an open sheet.
/// </summary>
/// <remarks>
/// A sheet is anchored to the bottom of its page, and a page inside a tab is inset above the tab bar.
/// With the bar visible a short sheet stopped at that inset while a taller sheet overflowed behind it,
/// so the same frame reached the bottom edge differently on two pages. Every sheet hides the bar while
/// it is open and restores the page's own value when the last one closes; the Today and Train pages
/// used to do this by hand per page.
/// </remarks>
internal static class ModalTabBar
{
    private static readonly ConditionalWeakTable<Page, OpenSheets> Open = new();

    /// <summary>Hides the tab bar while a sheet is open, remembering that this page hid it.</summary>
    public static void Acquire(Page? page)
    {
        if (page is null)
            return;

        var state = Open.GetOrCreateValue(page);
        if (state.Count == 0)
        {
            state.WasVisible = (bool)page.GetValue(Shell.TabBarIsVisibleProperty);
            Shell.SetTabBarIsVisible(page, false);
        }

        state.Count++;
    }

    /// <summary>Brings the tab bar back once the last sheet on that page has closed.</summary>
    public static void Release(Page? page)
    {
        if (page is null || !Open.TryGetValue(page, out var state))
            return;

        if (state.Count > 0)
            state.Count--;

        if (state.Count == 0)
            Shell.SetTabBarIsVisible(page, state.WasVisible);
    }

    /// <summary>How many sheets each page has open, so the tab bar returns exactly once rather than per sheet.</summary>
    private sealed class OpenSheets
    {
        public int Count;
        public bool WasVisible = true;
    }
}
