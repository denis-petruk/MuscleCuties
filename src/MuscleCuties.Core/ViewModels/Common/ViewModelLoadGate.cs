using MuscleCuties.Core.Diagnostics;

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
        AppDebugLog.Write("LoadGate", "Marked stale.");
        _lastLoadedAtUtc = DateTime.MinValue;
        _hasLoaded = false;
    }

    public async Task RunAsync(Func<Task> loadAsync, bool force = false)
    {
        AppDebugLog.Write("LoadGate", $"Run requested. force={force}, isFresh={IsFresh()}, currentCount={_semaphore.CurrentCount}.");

        if (!force && IsFresh())
        {
            AppDebugLog.Write("LoadGate", "Skipped because cached data is fresh.");
            return;
        }

        var waitStarted = DateTime.UtcNow;
        await _semaphore.WaitAsync();
        AppDebugLog.Write("LoadGate", $"Entered after {(DateTime.UtcNow - waitStarted).TotalMilliseconds:N0}ms.");

        try
        {
            if (!force && IsFresh())
            {
                AppDebugLog.Write("LoadGate", "Skipped after wait because cached data became fresh.");
                return;
            }

            AppDebugLog.Write("LoadGate", "Load started.");
            await loadAsync();
            AppDebugLog.Write("LoadGate", "Load completed.");

            _hasLoaded = true;
            _lastLoadedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("LoadGate", ex, "Load failed");
            throw;
        }
        finally
        {
            _semaphore.Release();
            AppDebugLog.Write("LoadGate", "Released.");
        }
    }

    private bool IsFresh() =>
        _hasLoaded &&
        DateTime.UtcNow - _lastLoadedAtUtc <= _freshnessWindow;
}
