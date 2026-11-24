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

    /// <summary>
    /// The one definition of "this page is still on its first load": the full-page indicator belongs
    /// to a busy page that has never completed a load. Eight view models computed this from their own
    /// busy flag plus either this gate's <see cref="HasLoaded"/> or a local flag, so the rule lived in
    /// eight places; a refresh after a successful load must never blank what is already on screen.
    /// </summary>
    public static bool IsLoading(bool isBusy, bool hasLoaded)
    {
        return isBusy && !hasLoaded;
    }

    public bool IsLoading(bool isBusy)
    {
        return IsLoading(isBusy, HasLoaded);
    }

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
                    Trace.WriteLine(
                        $"[PageLoad] {page.GetType().Name} marked failed ({ex.GetType().Name}): {ex.Message}");
                    throw;
                }
            }, force || page.IsLoadError);
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[PageLoad] {page.GetType().Name} failed ({exception.GetType().Name}): {exception.Message}");
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
            Trace.WriteLine($"[PageLoad] Refresh invalidated ({ex.GetType().Name}): {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// The single injury-change refresh path: publish the state the injury log now reports, mark the
    /// page stale, then reload it once, forced. Both the dashboard and the train page ran their own
    /// copy of this sequence.
    /// </summary>
    public Task RefreshAfterInjuryChangeAsync(
        bool hasActiveInjuries,
        Action<bool> applyHasActiveInjuries,
        Func<Task> loadAsync,
        IPageLoadAware page)
    {
        applyHasActiveInjuries(hasActiveInjuries);
        MarkStale();
        return RunAsync(loadAsync, page, true);
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
