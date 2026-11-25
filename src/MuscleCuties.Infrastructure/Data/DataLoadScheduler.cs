using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MuscleCuties.Core.Data;

/// <summary>Runs page loads off the UI thread, so a slow read cannot make the app look frozen.</summary>
public static class DataLoadScheduler
{
    /// <summary>Runs a load asynchronously, naming it by its caller so a failure can be traced to the page that asked.</summary>
    public static Task RunAsync(Func<Task> loadAsync, [CallerMemberName] string operation = "")
    {
        return RunAsync(async () =>
        {
            await loadAsync().ConfigureAwait(false);
            return true;
        }, operation);
    }

    /// <summary>The same for a load that returns a value.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> loadAsync, [CallerMemberName] string operation = "")
    {
        try
        {
            return await DatabaseOperationGate.RunAsync(loadAsync).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[DataLoad] {operation} failed ({exception.GetType().Name}).");
            // The caller owns UI state and must see the original failure.
            throw;
        }
    }
}
