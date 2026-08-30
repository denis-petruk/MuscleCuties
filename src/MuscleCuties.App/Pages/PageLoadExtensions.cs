using MuscleCuties.Core.Diagnostics;

namespace MuscleCuties.App.Pages;

internal static class PageLoadExtensions
{
    public static void LoadAfterFirstRender(this Page page, Func<Task> loadAsync)
    {
        page.Dispatcher.Dispatch(() => _ = RunSafelyAsync(loadAsync));
    }

    private static async Task RunSafelyAsync(Func<Task> loadAsync)
    {
        try
        {
            await Task.Yield();
            await loadAsync();
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("PageLoad", ex, "Deferred page load failed");
        }
    }
}
