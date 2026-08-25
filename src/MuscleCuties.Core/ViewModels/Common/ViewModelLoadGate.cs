namespace MuscleCuties.Core.ViewModels.Common;

internal sealed class ViewModelLoadGate
{
    private readonly TimeSpan _freshnessWindow;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DateTime _lastLoadedAtUtc = DateTime.MinValue;
    private bool _hasLoaded;

    public ViewModelLoadGate(TimeSpan freshnessWindow)
    {
        _freshnessWindow = freshnessWindow;
    }

    public void MarkStale()
    {
        _lastLoadedAtUtc = DateTime.MinValue;
        _hasLoaded = false;
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
        finally
        {
            _semaphore.Release();
        }
    }

    private bool IsFresh() =>
        _hasLoaded &&
        DateTime.UtcNow - _lastLoadedAtUtc <= _freshnessWindow;
}
