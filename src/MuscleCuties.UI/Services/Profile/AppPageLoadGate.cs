using System.Diagnostics;
using MuscleCuties.UI.ViewModels.Common;

namespace MuscleCuties.UI.Services.Profile;

internal sealed class AppPageLoadGate : IDisposable
{
    private const string ReportedLoadFailure = "Page load reported an error.";
    public static readonly TimeSpan PageFreshnessWindow = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _freshnessWindow;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DateTime _lastLoadedAtUtc = DateTime.MinValue;
    private int _revision;

    public AppPageLoadGate(TimeSpan freshnessWindow)
    {
        _freshnessWindow = freshnessWindow;
    }

    public bool HasLoaded { get; private set; }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    public void MarkStale()
    {
        Interlocked.Increment(ref _revision);
        _lastLoadedAtUtc = DateTime.MinValue;
        HasLoaded = false;
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
                    if (page.IsLoadError)
                        throw new InvalidOperationException(ReportedLoadFailure);
                }
                catch (Exception ex)
                {
                    page.IsLoadError = true;
                    Trace.WriteLine($"[PageLoad] {page.GetType().Name} marked failed ({ex.GetType().Name}).");
                    throw;
                }
            }, force || page.IsLoadError);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[PageLoad] {page.GetType().Name} failed ({exception.GetType().Name}).");
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

            var revision = _revision;
            await loadAsync();
            HasLoaded = revision == _revision;
            _lastLoadedAtUtc = HasLoaded ? DateTime.UtcNow : DateTime.MinValue;
        }
        catch (Exception ex)
        {
            // A failed refresh must not leave an old success marked fresh.
            HasLoaded = false;
            _lastLoadedAtUtc = DateTime.MinValue;
            Trace.WriteLine($"[PageLoad] Refresh invalidated ({ex.GetType().Name}).");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void InvalidateAndReloadIfToday(DateOnly changedDate, Func<Task> loadAsync, IPageLoadAware page)
    {
        MarkStale();
        if (changedDate == DateOnly.FromDateTime(DateTime.Today)) _ = RunAsync(loadAsync, page, true);
    }

    private bool IsFresh()
    {
        return HasLoaded &&
               DateTime.UtcNow - _lastLoadedAtUtc <= _freshnessWindow;
    }
}
