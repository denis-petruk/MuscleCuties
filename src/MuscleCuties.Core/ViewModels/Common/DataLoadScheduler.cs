namespace MuscleCuties.Core.ViewModels.Common;

internal static class DataLoadScheduler
{
    private static readonly SemaphoreSlim DatabaseGate = new(4, 4);

    public static async Task RunAsync(Func<Task> loadAsync)
    {
        await DatabaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(loadAsync).ConfigureAwait(false);
        }
        finally
        {
            DatabaseGate.Release();
        }
    }

    public static async Task<T> RunAsync<T>(Func<Task<T>> loadAsync)
    {
        await DatabaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(loadAsync).ConfigureAwait(false);
        }
        finally
        {
            DatabaseGate.Release();
        }
    }
}
