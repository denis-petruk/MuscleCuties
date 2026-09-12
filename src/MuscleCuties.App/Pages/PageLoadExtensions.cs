using System.Diagnostics;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.App.Pages;

internal static class PageLoadExtensions
{
    public static void InitializeWithTiming(this Page page, Action initialize)
    {
        var stopwatch = Stopwatch.StartNew();
        initialize();
        Trace.WriteLine(
            $"[Performance][{page.GetType().Name}] XAML initialized in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public static void BeginPageLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        Trace.WriteLine($"[Performance][{pageName}] Navigation completed; data load queued.");
        page.Dispatcher.Dispatch(() => _ = RunSafelyAsync(page, pageName, loadAsync));
    }

    private static async Task RunSafelyAsync(Page page, string pageName, Func<Task> loadAsync)
    {
        var stopwatch = Stopwatch.StartNew();
        Trace.WriteLine(
            $"[Performance][{pageName}] Data load started; size={page.Width:0}x{page.Height:0}.");

        if (page.BindingContext is IPageLoadAware loadAware)
            loadAware.IsLoadError = false;

        try
        {
            await loadAsync();
            Trace.WriteLine(
                $"[Performance][{pageName}] Data load completed in {stopwatch.ElapsedMilliseconds} ms; " +
                $"size={page.Width:0}x{page.Height:0}.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[Performance][{pageName}] Data load failed after {stopwatch.ElapsedMilliseconds} ms: {exception}");

            if (page.BindingContext is IPageLoadAware aware)
                aware.IsLoadError = true;
        }
    }
}
