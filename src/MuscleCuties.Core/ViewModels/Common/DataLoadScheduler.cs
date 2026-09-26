using System.Diagnostics;
using System.Runtime.CompilerServices;
using MuscleCuties.Core.Data;

namespace MuscleCuties.Core.ViewModels.Common;

public static class DataLoadScheduler
{
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
        try
        {
            return await DatabaseOperationGate.RunAsync(loadAsync).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[DataLoad] {operation} failed: {exception}");
            // The caller owns UI state and must see the original failure.
            throw;
        }
    }
}
