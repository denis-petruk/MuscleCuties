using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MuscleCuties.Core.ViewModels.Common;

public static class DataLoadScheduler
{
    // SQLite async APIs can execute synchronously. Keep that work off the UI
    // thread, but never overlap scheduled operations on the local database.
    private static readonly SemaphoreSlim DatabaseGate = new(1, 1);

    public static Task RunAsync(Func<Task> loadAsync, [CallerMemberName] string operation = "")
    {
        return RunAsync(async () =>
        {
            await loadAsync().ConfigureAwait(false);
            return true;
        }, operation);
    }

    public static async Task<T> RunAsync<T>(Func<Task<T>> loadAsync, [CallerMemberName] string operation = "")
    {
        await DatabaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(loadAsync).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[DataLoad] {operation} failed: {exception}");
            // The caller owns UI state and must see the original failure.
            throw;
        }
        finally
        {
            DatabaseGate.Release();
        }
    }
}
