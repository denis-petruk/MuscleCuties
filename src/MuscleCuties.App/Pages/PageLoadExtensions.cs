using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Views;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.App.Pages;

internal static class PageLoadExtensions
{
    private static readonly ConditionalWeakTable<Page, FirstLayoutTracker> FirstLayoutTrackers = new();

    public static async ValueTask LoadIfNeededAsync(this LazyView view, bool requested)
    {
        if (!requested || view.HasLazyViewLoaded)
            return;

        await view.LoadViewAsync();
        Trace.WriteLine($"[LazyView] {view.Content.GetType().Name} initialized.");
    }

    public static void BeginPageLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        _ = GetFirstLayoutTask(page);
        Trace.WriteLine($"[PageLoad][{pageName}] Data load started.");
        _ = RunSafelyAsync(page, pageName, loadAsync);
    }

    public static void BeginDeferredLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        var firstLayout = GetFirstLayoutTask(page);
        Trace.WriteLine($"[PageLoad][{pageName}] Deferred load queued.");
        _ = RunDeferredLoadSafelyAsync(page, pageName, firstLayout, loadAsync);
    }

    private static async Task RunDeferredLoadSafelyAsync(
        Page page,
        string pageName,
        Task firstLayout,
        Func<Task> loadAsync)
    {
        await firstLayout.ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => RunSafelyAsync(page, pageName, loadAsync));
    }

    private static async Task RunSafelyAsync(Page page, string pageName, Func<Task> loadAsync)
    {
        if (page.BindingContext is IPageLoadAware loadAware)
            loadAware.IsLoadError = false;

        try
        {
            await loadAsync();
            Trace.WriteLine($"[PageLoad][{pageName}] Load completed.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[PageLoad][{pageName}] Load failed: {exception}");

            if (page.BindingContext is IPageLoadAware aware)
                aware.IsLoadError = true;
        }
    }

    private static Task GetFirstLayoutTask(Page page)
    {
        if (page.Width > 0 && page.Height > 0)
        {
            Trace.WriteLine($"[PageLoad][{page.GetType().Name}] Already arranged.");
            return Task.CompletedTask;
        }

        var tracker = FirstLayoutTrackers.GetValue(page, static _ => new FirstLayoutTracker());
        if (tracker.IsSubscribed)
            return tracker.Completion.Task;

        tracker.IsSubscribed = true;
        page.SizeChanged += OnFirstLayout;
        CompleteIfArranged();
        return tracker.Completion.Task;

        void OnFirstLayout(object? sender, EventArgs args)
        {
            CompleteIfArranged();
        }

        void CompleteIfArranged()
        {
            if (page.Width <= 0 || page.Height <= 0 || tracker.Completion.Task.IsCompleted)
                return;

            page.SizeChanged -= OnFirstLayout;
            Trace.WriteLine($"[PageLoad][{page.GetType().Name}] First layout completed.");
            tracker.Completion.TrySetResult(true);
        }
    }

    private sealed class FirstLayoutTracker
    {
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSubscribed { get; set; }
    }
}
