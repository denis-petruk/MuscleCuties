using System.Diagnostics;

namespace MuscleCuties.Core.ViewModels.Common;

internal sealed class ViewModelLoadGate
{
    public static readonly TimeSpan PageFreshnessWindow = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _freshnessWindow;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _hasLoaded;
    private DateTime _lastLoadedAtUtc = DateTime.MinValue;

    public bool HasLoaded => _hasLoaded;

    public ViewModelLoadGate(TimeSpan freshnessWindow)
    {
        _freshnessWindow = freshnessWindow;
    }

    public void MarkStale()
    {
        _lastLoadedAtUtc = DateTime.MinValue;
        _hasLoaded = false;
    }

    public async Task RunAsync(Func<Task> loadAsync, IPageLoadAware page, bool force = false)
    {
        // This boundary is shared by preloads, page entry and the Retry command.
        // Await on the caller's context so observable state stays on the UI thread.
        try
        {
            await RunAsync(async () =>
            {
                page.IsLoadError = false;
                try
                {
                    await loadAsync();
                }
                catch
                {
                    page.IsLoadError = true;
                    throw;
                }
            }, force || page.IsLoadError);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[PageLoad] {page.GetType().Name} failed: {exception}");
        }
    }

    public async Task RunAsync(Func<Task> loadAsync, bool force = false)
    {
        if (!force && IsFresh())
            return;

        await _semaphore.WaitAsync();

        try
        {
            if (!force && IsFresh())
                return;

            await loadAsync();

            _hasLoaded = true;
            _lastLoadedAtUtc = DateTime.UtcNow;
        }
        catch
        {
            // A failed refresh must not leave an old success marked fresh.
            _lastLoadedAtUtc = DateTime.MinValue;
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool IsFresh()
    {
        return _hasLoaded &&
               DateTime.UtcNow - _lastLoadedAtUtc <= _freshnessWindow;
    }
}
